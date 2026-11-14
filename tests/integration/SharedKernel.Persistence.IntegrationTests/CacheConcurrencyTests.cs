using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Persistence.Caching;
using Shouldly;
using Testcontainers.Redis;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace SharedKernel.Persistence.IntegrationTests;

/// <summary>Verifies a cold cache fill is coordinated between independent hosts.</summary>
public sealed class CacheConcurrencyTests
{
    /// <summary>Production registration cannot accidentally connect directly to a standalone primary.</summary>
    [Fact]
    public void ServiceRegistrationRequiresSentinel()
    {
        var services = new ServiceCollection();
        _ = Should.Throw<ArgumentException>(() => services.AddFoundationRedis("localhost:6379")).ShouldBeOfType<ArgumentException>();
    }

    /// <summary>Two replicas share one factory result instead of simultaneously loading the source.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task ColdFillRunsOneFactoryAcrossHosts()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var redis = new RedisBuilder("redis:8-alpine").Build();
        await redis.StartAsync(cancellation).ConfigureAwait(true);
        var services = new ServiceCollection();
        _ = services.AddFoundationStandaloneRedis(redis.GetConnectionString());
        await using var first = services.BuildServiceProvider();
        var otherServices = new ServiceCollection();
        _ = otherServices.AddFoundationStandaloneRedis(redis.GetConnectionString());
        await using var second = otherServices.BuildServiceProvider();
        var left = first.GetRequiredService<IFusionCache>();
        var right = second.GetRequiredService<IFusionCache>();
        right.ShouldNotBeSameAs(left);
        _ = await right.TryGetAsync<string>("warm-connection", token: cancellation).ConfigureAwait(true);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executions = 0;
        async Task<string> LoadAsync(CancellationToken token)
        {
            _ = Interlocked.Increment(ref executions);
            _ = entered.TrySetResult();
            await release.Task.WaitAsync(token).ConfigureAwait(false);
            return "one-result";
        }

        var firstFill = left.GetOrSetAsync<string>("cold", (_, token) => LoadAsync(token), token: cancellation).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellation).ConfigureAwait(true);
        var secondFill = right.GetOrSetAsync<string>("cold", (_, token) => LoadAsync(token), token: cancellation).AsTask();
        await Task.Delay(500, cancellation).ConfigureAwait(true);
        release.SetResult();
        var results = await Task.WhenAll(firstFill, secondFill).ConfigureAwait(true);
        foreach (var result in results)
        {
            result.ShouldBe("one-result");
        }

        executions.ShouldBe(1);
    }
}
