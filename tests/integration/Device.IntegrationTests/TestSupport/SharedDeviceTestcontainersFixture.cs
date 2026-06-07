// <copyright file="SharedDeviceTestcontainersFixture.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Device.IntegrationTests.TestSupport;

public sealed class SharedDeviceTestcontainersFixture : IAsyncLifetime
{
    private readonly SemaphoreSlim databaseLock = new(1, 1);
    public PostgreSqlContainer? DbContainer { get; private set; }

    public RabbitMqContainer? RabbitContainer { get; private set; }

    public SharedDeviceTestcontainersFixture()
    {
        try
        {
            this.DbContainer = new PostgreSqlBuilder("postgres:latest")
                .WithDatabase("device_testdb")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .WithCommand("-c", "max_connections=500")
                .Build();

            this.RabbitContainer = new RabbitMqBuilder("rabbitmq:3-management")
                .WithUsername("guest")
                .WithPassword("guest")
                .Build();
        }
        catch (Exception ex) when (IsDockerUnavailable(ex))
        {
            this.DbContainer = null;
            this.RabbitContainer = null;
        }
    }

    /// <summary>
    /// Gets a value indicating whether both required containers are available
    /// (i.e. the local Docker daemon is reachable). Tests gate execution on this flag.
    /// </summary>
    public bool IsAvailable => this.DbContainer is not null && this.RabbitContainer is not null;

    public async ValueTask InitializeAsync()
    {
        if (this.DbContainer is null || this.RabbitContainer is null)
        {
            return;
        }

        // Postgres and RabbitMQ are independent - start them concurrently to halve fixture spin-up time.
        await Task.WhenAll(
            this.DbContainer.StartAsync(TestContext.Current.CancellationToken),
            this.RabbitContainer.StartAsync(TestContext.Current.CancellationToken)).ConfigureAwait(false);

        // Run EF migrations once per shared fixture so concurrent per-test host startups do not race.
        var dbContextOptions = new DbContextOptionsBuilder<Device.Infrastructure.Persistence.DeviceWriteDbContext>()
            .UseNpgsql(this.DbContainer.GetConnectionString(), npgsql => npgsql.MigrationsAssembly("Teck.Cloud.Migrations.PostgreSQL"))
            .Options;
        await using var dbContext = new Device.Infrastructure.Persistence.DeviceWriteDbContext(dbContextOptions);
        await dbContext.Database.MigrateAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (this.DbContainer is not null)
        {
            await this.DbContainer.DisposeAsync();
        }

        if (this.RabbitContainer is not null)
        {
            await this.RabbitContainer.DisposeAsync();
        }

        this.databaseLock.Dispose();
    }

    public string GetDatabaseConnectionString(string databaseName)
    {
        if (this.DbContainer is null)
        {
            throw new InvalidOperationException("PostgreSQL container is not available.");
        }

        var builder = new NpgsqlConnectionStringBuilder(this.DbContainer.GetConnectionString())
        {
            Database = databaseName,
            Pooling = true,
            MaxPoolSize = 50,
            ConnectionLifetime = 300,
        };

        return builder.ConnectionString;
    }

    public async Task<string> CreateSharedTestDatabaseAsync(
        Type dbContextType,
        string migrationsAssembly,
        CancellationToken cancellationToken = default)
    {
        if (this.DbContainer is null)
        {
            throw new InvalidOperationException("PostgreSQL container is not available.");
        }

        await this.databaseLock.WaitAsync(cancellationToken);
        try
        {
            string databaseName = $"testdb_{dbContextType.Name.ToLowerInvariant()}";

            await using var adminConnection = new NpgsqlConnection(this.DbContainer.GetConnectionString());
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
                    await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            return GetDatabaseConnectionString(databaseName);
        }
        finally
        {
            this.databaseLock.Release();
        }
    }

    public async Task TruncateAllTablesAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await this.databaseLock.WaitAsync(cancellationToken);
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
            this.databaseLock.Release();
        }
    }

    private static bool IsDockerUnavailable(Exception ex)
    {
        if (ex.Message.Contains("Docker is either not running or misconfigured", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Failed to connect to Docker endpoint", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("docker_engine", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ex.InnerException is not null && IsDockerUnavailable(ex.InnerException);
    }
}
