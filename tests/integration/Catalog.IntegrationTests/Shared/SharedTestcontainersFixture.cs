#pragma warning disable IDE0005
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

using Xunit;

namespace Catalog.IntegrationTests.Shared
{
    public class SharedTestcontainersFixture : IAsyncLifetime
    {
        public PostgreSqlContainer? DbContainer { get; private set; }
        public RabbitMqContainer? RabbitMqContainer { get; private set; }
        public bool UseSqliteFallback { get; private set; }
        public SqliteConnection? SqliteConnection { get; private set; }
        public SharedTestcontainersFixture()
        {
            TryConfigurePodmanSocketIfAvailable();

            try
            {
                DbContainer = new PostgreSqlBuilder("postgres:latest")
                    .WithDatabase("testdb")
                    .WithUsername("postgres")
                    .WithPassword("postgres")
                    .Build();

                RabbitMqContainer = RabbitMqTestContainerFactory.Create();
            }
            catch (Exception ex) when (IsDockerUnavailable(ex))
            {
                UseSqliteFallback = true;
            }
        }


        private static void TryConfigurePodmanSocketIfAvailable()
        {
            try
            {
                Console.WriteLine("[Testcontainers] Detecting Docker/Podman configuration...");
                Console.WriteLine($"[Testcontainers] OS: {Environment.OSVersion}");
                var currentOverride = Environment.GetEnvironmentVariable("TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE");
                var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
                Console.WriteLine($"[Testcontainers] DOCKER_HOST={dockerHost ?? "(not set)"}");
                Console.WriteLine($"[Testcontainers] TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE={currentOverride ?? "(not set)"}");

                // If already overridden, respect it
                if (!string.IsNullOrEmpty(currentOverride))
                {
                    Console.WriteLine("[Testcontainers] Using existing TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE.");
                    return;
                }

                if (string.IsNullOrEmpty(dockerHost))
                {
                    // Try common unix socket locations
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
                            var overrideValue = $"unix://{path}";
                            Environment.SetEnvironmentVariable("TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE", overrideValue);
                            Console.WriteLine($"[Testcontainers] Found socket at {path}; set TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE={overrideValue}");
                            return;
                        }
                    }

                    Console.WriteLine("[Testcontainers] No unix socket detected; leaving defaults.");
                    return;
                }

                // Forward unix socket if provided
                if (dockerHost.StartsWith("unix://", StringComparison.OrdinalIgnoreCase) || dockerHost.EndsWith(".sock", StringComparison.OrdinalIgnoreCase))
                {
                    Environment.SetEnvironmentVariable("TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE", dockerHost);
                    Console.WriteLine($"[Testcontainers] Forwarding DOCKER_HOST unix socket to TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE: {dockerHost}");
                    return;
                }

                // If DOCKER_HOST is a named pipe or Podman engine reference, inform the user
                if (dockerHost.StartsWith("npipe:", StringComparison.OrdinalIgnoreCase) || dockerHost.Contains("podman_engine", StringComparison.OrdinalIgnoreCase) || dockerHost.Contains("podman", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("[Testcontainers] Detected DOCKER_HOST using a Podman named pipe or Podman reference which Docker.DotNet may not support.");
                    Console.WriteLine("[Testcontainers] For Podman Desktop follow: https://podman-desktop.io/tutorial/testcontainers-with-podman to enable a unix socket and set TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE accordingly.");
                    return;
                }

                Console.WriteLine("[Testcontainers] No special Podman configuration detected; using default Docker endpoint.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Testcontainers] Podman detection failed: {ex.Message}");
            }
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
                // Increase retry attempts and delay to reduce flakes on slow machines/CI
                await RetryAsync(async () => await DbContainer!.StartAsync(), 5, TimeSpan.FromSeconds(3));
                var postgresConn = DbContainer!.GetConnectionString();
                Console.WriteLine($"[Testcontainers] Postgres started: {postgresConn}");
                // Expose postgres connection strings to the test host via environment variables
                Environment.SetEnvironmentVariable("ConnectionStrings__db-write", postgresConn);
                Environment.SetEnvironmentVariable("ConnectionStrings__db-read", postgresConn);

                await RetryAsync(async () => await RabbitMqContainer!.StartAsync(), 5, TimeSpan.FromSeconds(3));
                var rabbitConnRaw = RabbitMqContainer!.GetConnectionString() ?? string.Empty;
                // Normalize rabbitmq:// / rabbitmqs:// to amqp(s):// for RabbitMQ.Client compatibility
                var rabbitConn = rabbitConnRaw;
                if (rabbitConnRaw.StartsWith("rabbitmqs://", StringComparison.OrdinalIgnoreCase))
                {
                    rabbitConn = string.Concat("amqps://", rabbitConnRaw.AsSpan("rabbitmqs://".Length));
                }
                else if (rabbitConnRaw.StartsWith("rabbitmq://", StringComparison.OrdinalIgnoreCase))
                {
                    rabbitConn = string.Concat("amqp://", rabbitConnRaw.AsSpan("rabbitmq://".Length));
                }
                Console.WriteLine($"[Testcontainers] RabbitMQ started: {rabbitConn}");
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
                // Detect Docker/Podman named pipe incompatibility on Windows and provide actionable message
                if (ex.ToString().Contains("unsupported UNC path", StringComparison.OrdinalIgnoreCase) || ex.ToString().Contains("podman_engine", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Testcontainers failed to start containers because the Docker endpoint appears to be a Podman named pipe which is unsupported by Docker.DotNet. " +
                        "Please configure Podman to expose a Docker-compatible unix socket (e.g. /run/podman/podman.sock) and set the environment variable TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=unix:///run/podman/podman.sock, or use Docker Desktop.\n" +
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
                try { await SqliteConnection.DisposeAsync(); } catch { }
                return;
            }

            if (RabbitMqContainer is not null)
            {
                try { await RabbitMqContainer.DisposeAsync(); } catch { }
            }

            if (DbContainer is not null)
            {
                try { await DbContainer.DisposeAsync(); } catch { }
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
