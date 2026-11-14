using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Core.Domain;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Persistence.Caching;
using Shouldly;
using Testcontainers.Redis;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace SharedKernel.Persistence.IntegrationTests;

/// <summary>Exercises Redis-backed cache namespaces, value isolation and cross-client invalidation.</summary>
public sealed partial class CacheTests
{
    /// <summary>Global and tenant keys never collide and cached objects are detached copies.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task NamespacesAndCopiesAreIsolated()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        _ = services.AddFoundationCache();
        var provider = services.BuildServiceProvider();
        await using var disposeProvider = provider.ConfigureAwait(true);
        var cache = provider.GetRequiredService<IFusionCache>();
        var alpha = Create(cache, DataScope.ForTenant("alpha"));
        var beta = Create(cache, DataScope.ForTenant("beta"));
        var global = Create(cache, DataScope.Global);
        await alpha.SetAsync(1, new Projection { Name = "alpha" }, cancellation).ConfigureAwait(true);
        await beta.SetAsync(1, new Projection { Name = "beta" }, cancellation).ConfigureAwait(true);
        await global.SetAsync(1, new Projection { Name = "global" }, cancellation).ConfigureAwait(true);
        var copy = (await alpha.TryGetByIdAsync(1, cancellation).ConfigureAwait(true)).ShouldBeOfType<Projection>();
        copy.Name = "modified";
        ((await alpha.TryGetByIdAsync(1, cancellation).ConfigureAwait(true))?.Name).ShouldBe("alpha");
        ((await beta.TryGetByIdAsync(1, cancellation).ConfigureAwait(true))?.Name).ShouldBe("beta");
        ((await global.TryGetByIdAsync(1, cancellation).ConfigureAwait(true))?.Name).ShouldBe("global");
        new CacheNamespace("s", "c", "1", DataScope.ForTenant("a")).Key("b/c").ShouldNotBe(new CacheNamespace("s", "c", "1", DataScope.ForTenant("a/b")).Key("c"), StringComparer.Ordinal);
    }

    /// <summary>Independent hosts share Redis values and observe removals via the backplane.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task RedisBackplaneAndLocksWorkAcrossHosts()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var redis = new RedisBuilder("redis:8-alpine").Build();
        await using var disposeRedis = redis.ConfigureAwait(true);
        await redis.StartAsync(cancellation).ConfigureAwait(true);
        var first = Build(redis.GetConnectionString());
        await using var disposeFirst = first.ConfigureAwait(true);
        var second = Build(redis.GetConnectionString());
        await using var disposeSecond = second.ConfigureAwait(true);
        var firstCache = Create(first.GetRequiredService<IFusionCache>(), DataScope.ForTenant("alpha"));
        var secondCache = Create(second.GetRequiredService<IFusionCache>(), DataScope.ForTenant("alpha"));
        await firstCache.SetAsync(1, new Projection { Name = "shared" }, cancellation).ConfigureAwait(true);
        ((await secondCache.TryGetByIdAsync(1, cancellation).ConfigureAwait(true))?.Name).ShouldBe("shared");
        await firstCache.RemoveAsync(1, cancellation).ConfigureAwait(true);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        while (await secondCache.TryGetByIdAsync(1, deadline.Token).ConfigureAwait(true) is not null)
        {
            await Task.Delay(25, deadline.Token).ConfigureAwait(true);
        }

        await using var firstScope = first.CreateAsyncScope();
        await using var secondScope = second.CreateAsyncScope();
        var name = new CacheNamespace("service", "locks", "1", DataScope.ForTenant("alpha"));
        var lease = await firstScope.ServiceProvider.GetRequiredService<ScopedDistributedLock>()
            .AcquireAsync(name, "same", TimeSpan.FromSeconds(2), cancellation).ConfigureAwait(true);
        await using var disposeLease = lease.ConfigureAwait(true);
        _ = (await Should.ThrowAsync<TimeoutException>(async () =>
        {
            var other = await secondScope.ServiceProvider.GetRequiredService<ScopedDistributedLock>()
                .AcquireAsync(name, "same", TimeSpan.Zero, cancellation).ConfigureAwait(true);
            await other.DisposeAsync().ConfigureAwait(true);
        }).ConfigureAwait(true)).ShouldBeOfType<TimeoutException>();
    }

    /// <summary>A tenant-marked projection cannot enter another tenant's cache or a global namespace.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task TenantProjectionOwnershipIsChecked()
    {
        var services = new ServiceCollection();
        _ = services.AddFoundationCache();
        await using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IFusionCache>();
        var alpha = new GenericCacheService<TenantProjection, int>(cache, new CacheNamespace("service", "tenant-projection", "1", DataScope.ForTenant("alpha")), CacheJson.Default.TenantProjection, (_, _) => Task.FromResult<TenantProjection?>(new TenantProjection { TenantId = "beta" }), id => id.ToString(System.Globalization.CultureInfo.InvariantCulture), TimeSpan.FromMinutes(1));
        _ = (await Should.ThrowAsync<InvalidOperationException>(() => alpha.GetOrSetByIdAsync(1, TestContext.Current.CancellationToken)).ConfigureAwait(true)).ShouldBeOfType<InvalidOperationException>();
        _ = (await Should.ThrowAsync<InvalidOperationException>(() => alpha.SetAsync(1, new TenantProjection { TenantId = "beta" }, TestContext.Current.CancellationToken)).ConfigureAwait(true)).ShouldBeOfType<InvalidOperationException>();
        _ = Should.Throw<ArgumentException>(() => new GenericCacheService<TenantProjection, int>(cache, new CacheNamespace("service", "tenant-projection", "1", DataScope.Global), CacheJson.Default.TenantProjection, (_, _) => Task.FromResult<TenantProjection?>(null), id => id.ToString(System.Globalization.CultureInfo.InvariantCulture), TimeSpan.FromMinutes(1))).ShouldBeOfType<ArgumentException>();
    }

    private static ServiceProvider Build(string connection)
    {
        var services = new ServiceCollection();
        _ = services.AddFoundationStandaloneRedis(connection);
        return services.BuildServiceProvider();
    }

    private static GenericCacheService<Projection, int> Create(IFusionCache cache, DataScope scope) =>
        new(cache, new CacheNamespace("test-service", "projection", "1", scope), CacheJson.Default.Projection, (_, _) => Task.FromResult<Projection?>(null), id => id.ToString(System.Globalization.CultureInfo.InvariantCulture), TimeSpan.FromMinutes(1));

    /// <summary>A mutable projection used to verify cache copy isolation.</summary>
    public sealed class Projection
    {
        /// <summary>Gets or sets its display name.</summary>
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>A tenant-owned read projection.</summary>
    public sealed class TenantProjection : ITenantScoped
    {
        /// <inheritdoc/>
        public string TenantId { get; set; } = string.Empty;
    }

    [JsonSerializable(typeof(Projection))]
    [JsonSerializable(typeof(TenantProjection))]
    private sealed partial class CacheJson : JsonSerializerContext;
}
