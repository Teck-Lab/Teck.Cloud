using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Persistence.Caching;
using Shouldly;
using Testcontainers.Redis;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Verifies the read-through tenant store against real Keycloak and Redis.</summary>
/// <param name="fixture">The disposable Keycloak realm and identities.</param>
public sealed partial class KeycloakTenantStoreTests(KeycloakFixture fixture) : IClassFixture<KeycloakFixture>
{
    /// <summary>A second host reads the Redis entry without repeating the Keycloak request.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task IndependentHostsShareRedisCacheAndKeepCanonicalIdentity()
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
        var credentials = await fixture.AdminAuthorizationAsync(cancellation).ConfigureAwait(true);
        using var left = new KeycloakStoreClient(fixture.ServerAddress, credentials, first.GetRequiredService<IFusionCache>(), TimeProvider.System);
        using var right = new KeycloakStoreClient(fixture.ServerAddress, credentials, second.GetRequiredService<IFusionCache>(), TimeProvider.System);
        var tenant = (await left.Store.GetAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true)).ShouldNotBeNull();
        tenant.Id.ShouldBe(fixture.OrganizationId);
        tenant.Identifier.ShouldBe(fixture.OrganizationId);
        tenant.Name.ShouldBe("Fixture tenant");
        left.Handler.Count.ShouldBe(1);
        (await left.Store.GetByIdentifierAsync(fixture.OrganizationId).ConfigureAwait(true)).ShouldNotBeNull().Id.ShouldBe(tenant.Id);
        left.Handler.Count.ShouldBe(1);
        right.Handler.Unavailable = true;
        (await right.Store.GetAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true)).ShouldNotBeNull().Id.ShouldBe(tenant.Id);
        right.Handler.Count.ShouldBe(0);
        (await right.Store.GetByIdentifierAsync("fixture").ConfigureAwait(true)).ShouldBeNull();
        right.Handler.Count.ShouldBe(0);
        await VerifyBackplaneInvalidationAsync(left, right, cancellation).ConfigureAwait(true);
        using var third = new KeycloakStoreClient(fixture.ServerAddress, credentials, second.GetRequiredService<IFusionCache>(), TimeProvider.System, resource: "different-client");
        _ = (await third.Store.GetAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true)).ShouldNotBeNull();
        third.Handler.Count.ShouldBe(1);
    }

    /// <summary>Disabled organizations stop resolving at expiry and no stale active result survives an outage.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task ExpiryRechecksDisablementAndNeverServesStaleOnFailure()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var provider = MemoryCacheProvider();
        var clock = new AdvancingClock();
        using var client = new KeycloakStoreClient(fixture.ServerAddress, await fixture.AdminAuthorizationAsync(cancellation).ConfigureAwait(true), provider.GetRequiredService<IFusionCache>(), clock);
        (await client.Store.IsActiveAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true)).ShouldBeTrue();
        using var disabled = await fixture.UpdateOrganizationAsync(new JsonObject { ["enabled"] = false }, cancellation).ConfigureAwait(true);
        try
        {
            // The documented five-second decision window is deliberate, never sliding on reads.
            (await client.Store.IsActiveAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true)).ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(6));
            (await client.Store.IsActiveAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true)).ShouldBeFalse();
            client.Handler.Count.ShouldBe(2);
            using var enabled = await fixture.UpdateOrganizationAsync(new JsonObject { ["enabled"] = true }, cancellation).ConfigureAwait(true);
            await client.Store.InvalidateAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true);
            (await client.Store.IsActiveAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true)).ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(6));
            client.Handler.Unavailable = true;
            _ = await Should.ThrowAsync<HttpRequestException>(() => client.Store.GetAsync(fixture.OrganizationId, cancellation)).ConfigureAwait(true);
            client.Handler.Unavailable = false;
            _ = (await client.Store.GetAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true)).ShouldNotBeNull();
        }
        finally
        {
            using var restored = await fixture.UpdateOrganizationAsync(new JsonObject { ["enabled"] = true }, cancellation).ConfigureAwait(true);
        }
    }

    /// <summary>A still-present Redis snapshot cannot extend the absolute decision window in another process.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task ExpiredRedisSnapshotRechecksProviderBeforeGrantingTenant()
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
        var credentials = await fixture.AdminAuthorizationAsync(cancellation).ConfigureAwait(true);
        var clock = new AdvancingClock();
        using var left = new KeycloakStoreClient(fixture.ServerAddress, credentials, first.GetRequiredService<IFusionCache>(), TimeProvider.System);
        using var right = new KeycloakStoreClient(fixture.ServerAddress, credentials, second.GetRequiredService<IFusionCache>(), clock);
        (await left.Store.IsActiveAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true)).ShouldBeTrue();
        using var disabled = await fixture.UpdateOrganizationAsync(new JsonObject { ["enabled"] = false }, cancellation).ConfigureAwait(true);
        try
        {
            // Advancing the store clock leaves Redis's physical TTL untouched: the serialized bound must protect this read.
            clock.Advance(TimeSpan.FromSeconds(6));
            (await right.Store.IsActiveAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true)).ShouldBeFalse();
            right.Handler.Count.ShouldBe(1);
        }
        finally
        {
            using var restored = await fixture.UpdateOrganizationAsync(new JsonObject { ["enabled"] = true }, cancellation).ConfigureAwait(true);
        }
    }

    private static ServiceProvider MemoryCacheProvider()
    {
        var services = new ServiceCollection();
        _ = services.AddFoundationCache();
        return services.BuildServiceProvider();
    }

    private async Task VerifyBackplaneInvalidationAsync(KeycloakStoreClient left, KeycloakStoreClient right, CancellationToken cancellation)
    {
        right.Handler.Unavailable = false;
        using var renamed = await fixture.UpdateOrganizationAsync(new JsonObject { ["name"] = "Renamed fixture" }, cancellation).ConfigureAwait(true);
        try
        {
            await left.Store.InvalidateAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true);
            var until = DateTimeOffset.UtcNow.AddSeconds(3);
            string? name;
            do
            {
                name = (await right.Store.GetAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true))?.Name;
                if (!string.Equals(name, "Renamed fixture", StringComparison.Ordinal))
                {
                    await Task.Delay(20, cancellation).ConfigureAwait(true);
                }
            }
            while (!string.Equals(name, "Renamed fixture", StringComparison.Ordinal) && DateTimeOffset.UtcNow < until);

            name.ShouldBe("Renamed fixture");
            right.Handler.Count.ShouldBe(1);
        }
        finally
        {
            using var restored = await fixture.UpdateOrganizationAsync(new JsonObject { ["name"] = "Fixture tenant" }, cancellation).ConfigureAwait(true);
            await left.Store.InvalidateAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true);
        }
    }

    private sealed class AdvancingClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => _now;

        internal void Advance(TimeSpan duration) => _now += duration;
    }
}
