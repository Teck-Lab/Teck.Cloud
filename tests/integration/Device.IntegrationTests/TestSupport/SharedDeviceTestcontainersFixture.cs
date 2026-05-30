// <copyright file="SharedDeviceTestcontainersFixture.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Device.IntegrationTests.TestSupport;

public sealed class SharedDeviceTestcontainersFixture : IAsyncLifetime
{
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
