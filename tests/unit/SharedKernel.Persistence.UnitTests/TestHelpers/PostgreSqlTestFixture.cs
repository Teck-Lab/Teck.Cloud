using System.Net.Sockets;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace SharedKernel.Persistence.UnitTests.TestHelpers;

/// <summary>
/// Provides a PostgreSQL test container for database tests.
/// Uses a shared container instance across all tests to avoid container startup overhead.
/// Each test gets its own database for isolation.
/// </summary>
/// <typeref name="TContext">The DbContext type to test</typeref>
internal sealed class PostgreSqlTestFixture<TContext> : IAsyncLifetime
    where TContext : DbContext
{
    // Static shared container - initialized once across all test instances
    private static PostgreSqlContainer? _sharedContainer;
    private static readonly SemaphoreSlim _containerLock = new(1, 1);
    private static bool _containerInitialized;

    private SqliteConnection? _sqliteConnection;
    private bool _useSqliteFallback;
    private string? _databaseName;

    public string ConnectionString
    {
        get
        {
            if (_useSqliteFallback)
            {
                return _sqliteConnection?.ConnectionString ?? string.Empty;
            }

            if (_sharedContainer is null || _databaseName is null)
            {
                return string.Empty;
            }

            var builder = new NpgsqlConnectionStringBuilder(_sharedContainer.GetConnectionString())
            {
                Database = _databaseName,
                Pooling = true,
                MaxPoolSize = 20,
            };
            return builder.ConnectionString;
        }
    }

    public PostgreSqlTestFixture()
    {
        try
        {
            // Don't build container here - do it lazily in InitializeAsync
        }
        catch (Exception exception) when (IsDockerUnavailable(exception))
        {
            _useSqliteFallback = true;
        }
    }

    public DbContextOptions<TContext> CreateDbContextOptions()
    {
        if (_useSqliteFallback)
        {
            return new DbContextOptionsBuilder<TContext>()
                .UseSqlite(_sqliteConnection!)
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors()
                .Options;
        }

        return new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(ConnectionString)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .Options;
    }

    public async ValueTask InitializeAsync()
    {
        if (_useSqliteFallback)
        {
            _sqliteConnection = new SqliteConnection("Data Source=:memory:");
            await _sqliteConnection.OpenAsync(CancellationToken.None);
            return;
        }

        try
        {
            // Ensure shared container is started (only once)
            await InitializeSharedContainerAsync();

            // Create a unique database for this test
            _databaseName = $"testdb_{Guid.NewGuid():N}";
            await using var adminConnection = new NpgsqlConnection(_sharedContainer!.GetConnectionString());
            await adminConnection.OpenAsync(CancellationToken.None);
            await using var createCommand = adminConnection.CreateCommand();
            createCommand.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
            await createCommand.ExecuteNonQueryAsync(CancellationToken.None);
        }
        catch (Exception exception) when (IsDockerUnavailable(exception))
        {
            _useSqliteFallback = true;
            _sqliteConnection = new SqliteConnection("Data Source=:memory:");
            await _sqliteConnection.OpenAsync(CancellationToken.None);
        }
    }

    private static async Task InitializeSharedContainerAsync()
    {
        if (_containerInitialized)
        {
            return;
        }

        await _containerLock.WaitAsync();
        try
        {
            if (_containerInitialized)
            {
                return;
            }

            _sharedContainer = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("postgres")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .WithCommand("-c", "max_connections=500")
                .Build();

            await RetryAsync(async () => await _sharedContainer.StartAsync(), 5, TimeSpan.FromSeconds(3));
            await WaitUntilReadyAsync(CancellationToken.None);

            _containerInitialized = true;
        }
        finally
        {
            _containerLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_sqliteConnection is not null)
        {
            await _sqliteConnection.DisposeAsync();
            return;
        }

        // Drop the test database
        if (_sharedContainer is not null && _databaseName is not null)
        {
            try
            {
                await using var adminConnection = new NpgsqlConnection(_sharedContainer.GetConnectionString());
                await adminConnection.OpenAsync(CancellationToken.None);

                await using var terminateCommand = adminConnection.CreateCommand();
                terminateCommand.CommandText = $@"
                    SELECT pg_terminate_backend(pid) 
                    FROM pg_stat_activity 
                    WHERE datname = '{_databaseName}' 
                    AND pid <> pg_backend_pid()";
                await terminateCommand.ExecuteNonQueryAsync(CancellationToken.None);

                await using var dropCommand = adminConnection.CreateCommand();
                dropCommand.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\"";
                await dropCommand.ExecuteNonQueryAsync(CancellationToken.None);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    private static async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 10;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_sharedContainer!.GetConnectionString());
                await connection.OpenAsync(cancellationToken);

                await using var command = new NpgsqlCommand("SELECT 1", connection);
                await command.ExecuteScalarAsync(cancellationToken);

                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransientFailure(ex))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken);
            }
        }
    }

    private static async Task RetryAsync(Func<Task> action, int maxAttempts, TimeSpan delay)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                attempt++;
                await action();
                return;
            }
            catch when (attempt < maxAttempts)
            {
                Console.WriteLine($"[Testcontainers] Attempt {attempt} failed; retrying after {delay.TotalSeconds}s...");
                await Task.Delay(delay);
            }
        }
    }

    private static bool IsTransientFailure(Exception exception)
    {
        if (exception is TimeoutException or IOException or SocketException or NpgsqlException)
        {
            return true;
        }

        return exception.InnerException is not null && IsTransientFailure(exception.InnerException);
    }

    private static bool IsDockerUnavailable(Exception exception)
    {
        if (exception.Message.Contains("Docker is either not running or misconfigured", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("Failed to connect to Docker endpoint", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return exception.InnerException is not null && IsDockerUnavailable(exception.InnerException);
    }
}
