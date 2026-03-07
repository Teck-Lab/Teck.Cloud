using System.Net.Sockets;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace SharedKernel.Persistence.UnitTests.TestHelpers;

/// <summary>
/// Provides a PostgreSQL test container for database tests.
/// Uses xUnit's IAsyncLifetime for proper async initialization and cleanup.
/// </summary>
/// <typeparam name="TContext">The DbContext type to test</typeparam>
internal sealed class PostgreSqlTestFixture<TContext> : IAsyncLifetime
    where TContext : DbContext
{
    private readonly PostgreSqlContainer? _container;
    private SqliteConnection? _sqliteConnection;
    private bool _useSqliteFallback;

    public PostgreSqlTestFixture()
    {
        try
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("testdb")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .WithCleanUp(true)
                .Build();
        }
        catch (Exception exception) when (IsDockerUnavailable(exception))
        {
            _useSqliteFallback = true;
        }
    }

    public string ConnectionString => _container?.GetConnectionString() ?? string.Empty;

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
            await _container!.StartAsync();
            await WaitUntilReadyAsync(CancellationToken.None);
        }
        catch (Exception exception) when (IsDockerUnavailable(exception))
        {
            _useSqliteFallback = true;
            _sqliteConnection = new SqliteConnection("Data Source=:memory:");
            await _sqliteConnection.OpenAsync(CancellationToken.None);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_sqliteConnection is not null)
        {
            await _sqliteConnection.DisposeAsync();
            return;
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 10;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var connection = new NpgsqlConnection(ConnectionString);
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
