#pragma warning disable IDE0005
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace Catalog.IntegrationTests.Shared
{
    public class SharedTestcontainersFixture : IAsyncLifetime
    {
        private readonly PostgreSqlContainer? dbContainer;
        private readonly RabbitMqContainer? rabbitMqContainer;
        private readonly SemaphoreSlim databaseLock = new(1, 1);
        public bool UseSqliteFallback { get; private set; }
        public SqliteConnection? SqliteConnection { get; private set; }

        public PostgreSqlContainer DbContainer
        {
            get
            {
                if (dbContainer is null)
                {
                    throw new InvalidOperationException("PostgreSQL container is not available.");
                }

                return dbContainer;
            }
        }

        public RabbitMqContainer RabbitMqContainer
        {
            get
            {
                if (rabbitMqContainer is null)
                {
                    throw new InvalidOperationException("RabbitMQ container is not available.");
                }

                return rabbitMqContainer;
            }
        }

        public string AdminConnectionString => DbContainer.GetConnectionString();

        public SharedTestcontainersFixture()
        {
            TryConfigurePodmanSocketIfAvailable();

            try
            {
                dbContainer = new PostgreSqlBuilder("postgres:latest")
                    .WithDatabase("postgres")
                    .WithUsername("postgres")
                    .WithPassword("postgres")
                    .WithCommand("-c", "max_connections=500")
                    .Build();

                rabbitMqContainer = RabbitMqTestContainerFactory.Create();
            }
            catch (Exception ex) when (IsDockerUnavailable(ex))
            {
                UseSqliteFallback = true;
            }
        }

        public string GetDatabaseConnectionString(string databaseName)
        {
            var builder = new NpgsqlConnectionStringBuilder(AdminConnectionString)
            {
                Database = databaseName,
                Pooling = false,
            };
            return builder.ConnectionString;
        }

        public async Task<string> CreateTestDatabaseAsync(
            Type dbContextType,
            string migrationsAssembly,
            CancellationToken cancellationToken = default)
        {
            string databaseName = $"test_{Guid.NewGuid():N}";

            await using var adminConnection = new NpgsqlConnection(AdminConnectionString);
            await adminConnection.OpenAsync(cancellationToken);
            await using var createCommand = adminConnection.CreateCommand();
            createCommand.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await createCommand.ExecuteNonQueryAsync(cancellationToken);

            string testConnectionString = GetDatabaseConnectionString(databaseName);

            var optionsBuilderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(dbContextType);
            var optionsBuilder = (DbContextOptionsBuilder)Activator.CreateInstance(optionsBuilderType)!;
            optionsBuilder.UseNpgsql(testConnectionString, npgsql => npgsql.MigrationsAssembly(migrationsAssembly));

            var dbContext = (DbContext)Activator.CreateInstance(dbContextType, optionsBuilder.Options, null)!;
            await using (dbContext)
            {
                // Catalog's read context (ApplicationReadDbContext) does not have its own migration set,
                // while the write context migrations create the shared schema. EnsureCreated creates the
                // schema from the current EF model for both contexts, which is sufficient for isolated
                // per-test databases and still validates PostgreSQL compatibility.
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            }

            return testConnectionString;
        }

        public async Task<string> CreateSharedTestDatabaseAsync(
            Type dbContextType,
            string migrationsAssembly,
            CancellationToken cancellationToken = default)
        {
            if (UseSqliteFallback)
            {
                if (SqliteConnection is null)
                {
                    SqliteConnection = new SqliteConnection("Data Source=:memory:");
                    await SqliteConnection.OpenAsync(cancellationToken);
                }

                return SqliteConnection.ConnectionString;
            }

            await databaseLock.WaitAsync(cancellationToken);
            try
            {
                string databaseName = $"testdb_{dbContextType.Name.ToLowerInvariant()}";

                await using var adminConnection = new NpgsqlConnection(AdminConnectionString);
                await adminConnection.OpenAsync(cancellationToken);
                await using var checkCommand = adminConnection.CreateCommand();
                checkCommand.CommandText = $"SELECT 1 FROM pg_database WHERE datname = '{databaseName}'";
                var exists = await checkCommand.ExecuteScalarAsync(cancellationToken) is not null;

                if (!exists)
                {
                    await using var createCommand = adminConnection.CreateCommand();
                    createCommand.CommandText = $"CREATE DATABASE \"{databaseName}\"";
                    await createCommand.ExecuteNonQueryAsync(cancellationToken);

                    string testConnectionString = GetDatabaseConnectionString(databaseName);
                    var optionsBuilderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(dbContextType);
                    var optionsBuilder = (DbContextOptionsBuilder)Activator.CreateInstance(optionsBuilderType)!;
                    optionsBuilder.UseNpgsql(testConnectionString, npgsql => npgsql.MigrationsAssembly(migrationsAssembly));

                    var dbContext = (DbContext)Activator.CreateInstance(dbContextType, optionsBuilder.Options, null)!;
                    await using (dbContext)
                    {
                        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
                    }
                }

                return GetDatabaseConnectionString(databaseName);
            }
            finally
            {
                databaseLock.Release();
            }
        }

        public async Task TruncateAllTablesAsync(string connectionString, CancellationToken cancellationToken = default)
        {
            if (UseSqliteFallback)
            {
                return;
            }

            await databaseLock.WaitAsync(cancellationToken);
            try
            {
                var cleanupConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
                {
                    Pooling = false,
                }.ConnectionString;

                await using var connection = new NpgsqlConnection(cleanupConnectionString);
                await connection.OpenAsync(cancellationToken);

                await using var truncateCommand = connection.CreateCommand();
                truncateCommand.CommandText = @"
                    DO $$
                    DECLARE
                        table_names text;
                    BEGIN
                        SELECT string_agg(quote_ident(tablename), ', ')
                        INTO table_names
                        FROM pg_tables
                        WHERE schemaname = 'public'
                        AND tablename != '__EFMigrationsHistory';

                        IF table_names IS NOT NULL THEN
                            EXECUTE 'TRUNCATE TABLE ' || table_names || ' RESTART IDENTITY CASCADE';
                        END IF;
                    END $$;";
                await truncateCommand.ExecuteNonQueryAsync(cancellationToken);
            }
            finally
            {
                databaseLock.Release();
            }
        }

        public async Task DropTestDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
        {
            await using var adminConnection = new NpgsqlConnection(AdminConnectionString);
            await adminConnection.OpenAsync(cancellationToken);

            await using var terminateCommand = adminConnection.CreateCommand();
            terminateCommand.CommandText = $@"
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = '{databaseName}'
                AND pid <> pg_backend_pid()";
            await terminateCommand.ExecuteNonQueryAsync(cancellationToken);

            await using var dropCommand = adminConnection.CreateCommand();
            dropCommand.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\"";
            await dropCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        private static void TryConfigurePodmanSocketIfAvailable()
        {
            // ... existing implementation
            try
            {
                Console.WriteLine("[Testcontainers] Detecting Docker/Podman configuration...");
                Console.WriteLine($"[Testcontainers] OS: {Environment.OSVersion}");
                var currentOverride = Environment.GetEnvironmentVariable("TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE");
                var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
                Console.WriteLine($"[Testcontainers] DOCKER_HOST={dockerHost ?? "(not set)"}");
                Console.WriteLine($"[Testcontainers] TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE={currentOverride ?? "(not set)"}");

                if (!string.IsNullOrEmpty(currentOverride))
                {
                    var normalizedOverride = NormalizeDockerSocketPath(currentOverride);
                    if (!string.Equals(currentOverride, normalizedOverride, StringComparison.Ordinal))
                    {
                        Environment.SetEnvironmentVariable("TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE", normalizedOverride);
                        Console.WriteLine($"[Testcontainers] Normalized TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE to {normalizedOverride}");
                    }

                    Console.WriteLine("[Testcontainers] Using existing TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE.");
                    return;
                }

                if (string.IsNullOrEmpty(dockerHost))
                {
                    string[] possiblePaths = new[]
                    {
                        "/run/podman/podman.sock",
                        "/var/run/docker.sock"
                    };

                    foreach (var path in possiblePaths)
                    {
                        Console.WriteLine($"[Testcontainers] Checking socket: {path}");
                        if (File.Exists(path))
                        {
                            var overrideValue = NormalizeDockerSocketPath(path);
                            Environment.SetEnvironmentVariable("TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE", overrideValue);
                            Console.WriteLine($"[Testcontainers] Found socket at {path}; set TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE={overrideValue}");
                            return;
                        }
                    }

                    Console.WriteLine("[Testcontainers] No unix socket detected; leaving defaults.");
                    return;
                }

                if (dockerHost.StartsWith("unix://", StringComparison.OrdinalIgnoreCase) || dockerHost.EndsWith(".sock", StringComparison.OrdinalIgnoreCase))
                {
                    var overrideValue = NormalizeDockerSocketPath(dockerHost);
                    Environment.SetEnvironmentVariable("TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE", overrideValue);
                    Console.WriteLine($"[Testcontainers] Forwarding DOCKER_HOST unix socket to TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE: {overrideValue}");
                    return;
                }

                if (dockerHost.StartsWith("npipe:", StringComparison.OrdinalIgnoreCase) || dockerHost.Contains("podman_engine", StringComparison.OrdinalIgnoreCase) || dockerHost.Contains("podman", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("[Testcontainers] Detected DOCKER_HOST using a Podman named pipe or Podman reference which Docker.DotNet may not support.");
                    Console.WriteLine("[Testcontainers] For Podman Desktop follow: https://podman-desktop.io/tutorial/testcontainers-with-podman to enable a Docker-compatible unix socket (e.g. /run/podman/podman.sock) and set the environment variable TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/run/podman/podman.sock, or use Docker Desktop.");
                    return;
                }

                Console.WriteLine("[Testcontainers] No special Podman configuration detected; using default Docker endpoint.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Testcontainers] Podman detection failed: {ex.Message}");
            }
        }

        private static string NormalizeDockerSocketPath(string value)
        {
            var normalized = value.Trim();
            if (!normalized.StartsWith("unix://", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            if (Uri.TryCreate(normalized, UriKind.Absolute, out Uri? parsed) &&
                string.Equals(parsed.Scheme, "unix", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(parsed.AbsolutePath))
            {
                return parsed.AbsolutePath;
            }

            var withoutScheme = normalized.Substring("unix://".Length);
            return withoutScheme.StartsWith('/')
                ? withoutScheme
                : "/" + withoutScheme.TrimStart('/');
        }

        public async ValueTask InitializeAsync()
        {
            if (UseSqliteFallback)
            {
                if (SqliteConnection is null)
                {
                    SqliteConnection = new SqliteConnection("Data Source=:memory:");
                    await SqliteConnection.OpenAsync();
                }

                Environment.SetEnvironmentVariable("ConnectionStrings__db-write", SqliteConnection.ConnectionString);
                Environment.SetEnvironmentVariable("ConnectionStrings__db-read", SqliteConnection.ConnectionString);
                Environment.SetEnvironmentVariable("ConnectionStrings__rabbitmq", string.Empty);
                return;
            }

            try
            {
                await RetryAsync(async () => await DbContainer.StartAsync(), 5, TimeSpan.FromSeconds(3));
                var postgresConn = DbContainer.GetConnectionString();
                Console.WriteLine($"[Testcontainers] Postgres started: {postgresConn}");
                Environment.SetEnvironmentVariable("ConnectionStrings__db-write", postgresConn);
                Environment.SetEnvironmentVariable("ConnectionStrings__db-read", postgresConn);

                await RetryAsync(async () => await RabbitMqContainer.StartAsync(), 5, TimeSpan.FromSeconds(3));
                var rabbitConnRaw = RabbitMqContainer.GetConnectionString() ?? string.Empty;
                var rabbitConn = rabbitConnRaw;
                if (rabbitConnRaw.StartsWith("rabbitmqs://", StringComparison.OrdinalIgnoreCase))
                {
                    rabbitConn = string.Concat("amqps://", rabbitConnRaw.AsSpan("rabbitmqs://".Length));
                }
                else if (rabbitConnRaw.StartsWith("rabbitmq://", StringComparison.OrdinalIgnoreCase))
                {
                    rabbitConn = string.Concat("amqp://", rabbitConnRaw.AsSpan("rabbitmq://".Length));
                }
                Console.WriteLine($"[Testcontainers] Rabbit started: {rabbitConn}");
                Environment.SetEnvironmentVariable("ConnectionStrings__rabbitmq", rabbitConn);
            }
            catch (Exception ex) when (IsDockerUnavailable(ex))
            {
                UseSqliteFallback = true;
                if (SqliteConnection is null)
                {
                    SqliteConnection = new SqliteConnection("Data Source=:memory:");
                    await SqliteConnection.OpenAsync();
                }

                Environment.SetEnvironmentVariable("ConnectionStrings__db-write", SqliteConnection.ConnectionString);
                Environment.SetEnvironmentVariable("ConnectionStrings__db-read", SqliteConnection.ConnectionString);
                Environment.SetEnvironmentVariable("ConnectionStrings__rabbitmq", string.Empty);
            }
            catch (Exception ex)
            {
                if (ex.ToString().Contains("unsupported UNC path", StringComparison.OrdinalIgnoreCase) || ex.ToString().Contains("podman_engine", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Testcontainers failed to start containers because the Docker endpoint appears to be a Podman named pipe which is unsupported by Docker.DotNet. " +
                        "Please configure Podman to expose a Docker-compatible unix socket (e.g. /run/podman/podman.sock) and set the environment variable TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/run/podman/podman.sock, or use Docker Desktop.\n" +
                        "Original error: " + ex.Message,
                        ex);
                }

                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (UseSqliteFallback && SqliteConnection is not null)
            {
                try { await SqliteConnection.DisposeAsync(); }
                catch { }
                return;
            }

            if (rabbitMqContainer is not null)
            {
                try { await rabbitMqContainer.DisposeAsync(); }
                catch { }
            }

            if (dbContainer is not null)
            {
                try { await dbContainer.DisposeAsync(); }
                catch { }
            }

            databaseLock.Dispose();
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

        private static bool IsDockerUnavailable(Exception ex)
        {
            if (ex.Message.Contains("Docker is either not running or misconfigured", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("Failed to connect to Docker endpoint", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("docker_engine", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return ex.InnerException is not null && IsDockerUnavailable(ex.InnerException);
        }
    }
}
