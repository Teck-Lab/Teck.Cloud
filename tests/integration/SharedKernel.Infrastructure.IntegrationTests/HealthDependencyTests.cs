using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharedKernel.Core.MultiTenancy;
using Shouldly;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Xunit;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Verifies mandatory checks using real service dependencies and Wolverine listeners.</summary>
[Collection(TelemetryGroup.Name)]
public sealed class HealthDependencyTests
{
    /// <summary>Dependency outages fail readiness, recover and never fail process liveness.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task DependenciesFailAndRecoverWithoutAHostRestart()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var database = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await using var rabbit = new RabbitMqBuilder("rabbitmq:4-alpine").Build();
        await using var redis = new RedisBuilder("redis:8-alpine").Build();
        await Task.WhenAll(database.StartAsync(cancellation), rabbit.StartAsync(cancellation), redis.StartAsync(cancellation)).ConfigureAwait(true);
        using var host = MessagingTests.BuildHost(database.GetConnectionString(), rabbit.GetConnectionString(), redis.GetConnectionString(), builder => KeycloakHealthTests.Configure(builder, new KeycloakHealthTests.DiscoveryState()));
        await using (var scope = host.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<OperationContext>().Initialize(DataScope.Global);
            _ = await scope.ServiceProvider.GetRequiredService<MessageDbContext>().Database.EnsureCreatedAsync(cancellation).ConfigureAwait(true);
        }

        var checks = host.Services.GetRequiredService<HealthCheckService>();
        (await checks.CheckHealthAsync(registration => registration.Tags.Contains("startup"), cancellation).ConfigureAwait(true)).Status.ShouldBe(HealthStatus.Unhealthy);
        await host.StartAsync(cancellation).ConfigureAwait(true);
        try
        {
            var ready = await checks.CheckHealthAsync(registration => registration.Tags.Contains("ready"), cancellation).ConfigureAwait(true);
            foreach (var entry in ready.Entries)
            {
                (entry.Value.Status == HealthStatus.Healthy).ShouldBeTrue(entry.Key + ": " + entry.Value.Description);
            }

            ready.Entries.Keys.ShouldContain("postgres-messaging", StringComparer.Ordinal);
            foreach (var (container, name) in new (IContainer Container, string Name)[] { (redis, "redis"), (rabbit, "rabbitmq"), (database, "postgres-messaging") })
            {
                // Keep the endpoint stable: Docker can remap random host ports after stop/start.
                // Pausing simulates an unavailable dependency without changing client configuration.
                await container.PauseAsync(cancellation).ConfigureAwait(true);
                try
                {
                    var offline = await checks.CheckHealthAsync(registration => string.Equals(registration.Name, name, StringComparison.Ordinal), cancellation).ConfigureAwait(true);
                    offline.Status.ShouldBe(HealthStatus.Unhealthy);
                    (await checks.CheckHealthAsync(registration => registration.Tags.Contains("live"), cancellation).ConfigureAwait(true)).Status.ShouldBe(HealthStatus.Healthy);
                }
                finally
                {
                    using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    await container.UnpauseAsync(recovery.Token).ConfigureAwait(true);
                }

                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                deadline.CancelAfter(TimeSpan.FromSeconds(45));
                while ((await checks.CheckHealthAsync(registration => string.Equals(registration.Name, name, StringComparison.Ordinal), deadline.Token).ConfigureAwait(true)).Status != HealthStatus.Healthy)
                {
                    await Task.Delay(250, deadline.Token).ConfigureAwait(true);
                }
            }

            (await checks.CheckHealthAsync(registration => registration.Tags.Contains("ready"), cancellation).ConfigureAwait(true)).Status.ShouldBe(HealthStatus.Healthy);
        }
        finally
        {
            await host.StopAsync(cancellation).ConfigureAwait(true);
        }

        (await checks.CheckHealthAsync(registration => string.Equals(registration.Name, "application", StringComparison.Ordinal) || registration.Tags.Contains("startup"), cancellation).ConfigureAwait(true)).Status.ShouldBe(HealthStatus.Unhealthy);
    }
}
