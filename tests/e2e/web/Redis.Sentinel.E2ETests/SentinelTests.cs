using System.Security.Cryptography;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using SharedKernel.Infrastructure.Hosting;
using SharedKernel.Persistence.Caching;
using Shouldly;
using StackExchange.Redis;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Redis.Sentinel.E2ETests;

/// <summary>Runs the real Aspire Sentinel topology and kills its primary without restarting consumers.</summary>
public sealed class SentinelTests
{
    /// <summary>Discovery, replica promotion and existing shared client recovery work end to end.</summary>
    /// <returns>The verification task.</returns>
    /// <exception cref="InvalidOperationException">The advertised host or connection resource is missing.</exception>
    [Fact]
    public async Task ExistingConsumersRecoverAfterPrimaryLoss()
    {
        var cancellation = TestContext.Current.CancellationToken;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        var hostAddress = Environment.GetEnvironmentVariable("TECK_REDIS_ADVERTISED_HOST")
            ?? throw new InvalidOperationException("Set TECK_REDIS_ADVERTISED_HOST to an address reachable by both host processes and Redis containers.");
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Teck_Cloud_AppHost>(
            ["--Profile=sentinel", "--Parameters:redis-advertised-host=" + hostAddress, "--Parameters:redis-password=" + password], deadline.Token).ConfigureAwait(true);
        await using var app = await builder.BuildAsync(deadline.Token).ConfigureAwait(true);
        await app.StartAsync(deadline.Token).ConfigureAwait(true);
        var configuration = await app.GetConnectionStringAsync("redis", deadline.Token).AsTask().WaitAsync(deadline.Token).ConfigureAwait(true)
            ?? throw new InvalidOperationException("Sentinel connection resource is missing.");
        using var first = BuildHost(configuration);
        using var second = BuildHost(configuration);
        var connection = await first.Services.GetRequiredService<RedisConnection>().GetAsync(deadline.Token).ConfigureAwait(true);
        var data = connection.GetDatabase();
        while (!connection.IsConnected)
        {
            await Task.Delay(250, deadline.Token).ConfigureAwait(true);
        }

        (await data.StringSetAsync("replication-proof", "preserved").WaitAsync(deadline.Token).ConfigureAwait(true)).ShouldBeTrue();
        await WaitForReplicaAsync(data, deadline.Token).ConfigureAwait(true);
        var primary = await data.IdentifyEndpointAsync(flags: CommandFlags.DemandMaster).WaitAsync(deadline.Token).ConfigureAwait(true);
        _ = primary.ShouldNotBeNull();
        await WaitForTopologyAsync(configuration, deadline.Token).ConfigureAwait(true);
        await StopPrimaryAsync(configuration, deadline.Token).ConfigureAwait(true);

        var cache = first.Services.GetRequiredService<IFusionCache>();
        var other = second.Services.GetRequiredService<IFusionCache>();
        var attempts = 0;
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            try
            {
                var current = await data.IdentifyEndpointAsync(flags: CommandFlags.DemandMaster).WaitAsync(deadline.Token).ConfigureAwait(true);
                if (!Equals(primary, current) && await data.StringGetAsync("replication-proof").WaitAsync(deadline.Token).ConfigureAwait(true) == "preserved")
                {
                    var key = "after-failover-" + (++attempts).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    await cache.SetAsync(key, "recovered", token: deadline.Token).ConfigureAwait(true);
                    if ((await other.TryGetAsync<string>(key, token: deadline.Token).ConfigureAwait(true)).HasValue)
                    {
                        break;
                    }
                }
            }
            catch (RedisException)
            {
                // Transient failures are expected while Sentinel elects and promotes the replica.
            }

            await Task.Delay(250, deadline.Token).ConfigureAwait(true);
        }

        (await first.Services.GetRequiredService<RedisConnection>().GetAsync(deadline.Token).ConfigureAwait(true)).ShouldBeSameAs(connection);
        await WaitForHealthAsync(first.Services.GetRequiredService<HealthCheckService>(), deadline.Token).ConfigureAwait(true);
    }

    private static async Task WaitForHealthAsync(HealthCheckService checks, CancellationToken cancellation)
    {
        while (true)
        {
            var health = await checks.CheckHealthAsync(registration => string.Equals(registration.Name, "redis", StringComparison.Ordinal), cancellation).ConfigureAwait(false);
            if (health.Status == HealthStatus.Healthy)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellation).ConfigureAwait(false);
        }
    }

    private static async Task WaitForReplicaAsync(IDatabase data, CancellationToken cancellation)
    {
        while ((int)await data.ExecuteAsync("WAIT", 1, 1000).WaitAsync(cancellation).ConfigureAwait(false) != 1)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellation).ConfigureAwait(false);
        }
    }

    private static async Task WaitForTopologyAsync(string configuration, CancellationToken cancellation)
    {
        var options = ConfigurationOptions.Parse(configuration);
        options.Password = options.SentinelPassword;
        options.User = options.SentinelUser;
        var monitor = await ConnectionMultiplexer.SentinelConnectAsync(options).WaitAsync(cancellation).ConfigureAwait(false);
        await using var disposeMonitor = monitor.ConfigureAwait(false);
        foreach (var endpoint in options.EndPoints)
        {
            var server = monitor.GetServer(endpoint);
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                var replicas = await server.SentinelReplicasAsync(options.ServiceName!).WaitAsync(cancellation).ConfigureAwait(false);
                var peers = await server.SentinelSentinelsAsync(options.ServiceName!).WaitAsync(cancellation).ConfigureAwait(false);
                var replica = replicas.FirstOrDefault()?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                if (peers.Length == 2 && replica is not null &&
                    string.Equals(replica.GetValueOrDefault("master-link-status"), "ok", StringComparison.Ordinal) &&
                    string.Equals(replica.GetValueOrDefault("flags"), "slave", StringComparison.Ordinal))
                {
                    _ = await server.ExecuteAsync("SENTINEL", "CKQUORUM", options.ServiceName!).WaitAsync(cancellation).ConfigureAwait(false);
                    break;
                }

                await Task.Delay(250, cancellation).ConfigureAwait(false);
            }
        }
    }

    private static async Task StopPrimaryAsync(string configuration, CancellationToken cancellation)
    {
        var adminOptions = ConfigurationOptions.Parse(configuration);
        adminOptions.AllowAdmin = true;
        var admin = await ConnectionMultiplexer.ConnectAsync(adminOptions).WaitAsync(cancellation).ConfigureAwait(false);
        await using var disposeAdmin = admin.ConfigureAwait(false);
        try
        {
            _ = await admin.GetDatabase().ExecuteAsync("SHUTDOWN", "NOSAVE").WaitAsync(cancellation).ConfigureAwait(false);
        }
        catch (RedisConnectionException)
        {
            // SHUTDOWN can close its connection before returning a response.
        }
    }

    private static IHost BuildHost(string configuration)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddFoundationHost(exportTelemetry: false);
        _ = builder.Services.AddFoundationRedis(configuration);
        return builder.Build();
    }
}
