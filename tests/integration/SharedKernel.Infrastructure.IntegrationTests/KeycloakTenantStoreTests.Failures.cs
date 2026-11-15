using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions;
using SharedKernel.Infrastructure.MultiTenancy;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Failure and identity boundaries for the actual SDK-backed cache.</summary>
public sealed partial class KeycloakTenantStoreTests
{
    /// <summary>Missing organizations are negative-cached; unauthorized responses are errors and never cached as absence.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task MissingTenantsAndAuthenticationFailuresHaveDifferentSemantics()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var provider = MemoryCacheProvider();
        var cache = provider.GetRequiredService<IFusionCache>();
        using var client = new KeycloakStoreClient(fixture.ServerAddress, await fixture.AdminAuthorizationAsync(cancellation).ConfigureAwait(true), cache, TimeProvider.System);
        var missing = Guid.NewGuid().ToString("D");
        (await client.Store.GetAsync(missing, cancellation).ConfigureAwait(true)).ShouldBeNull();
        (await client.Store.IsActiveAsync(missing, cancellation).ConfigureAwait(true)).ShouldBeFalse();
        client.Handler.Count.ShouldBe(1);
        using var unauthorized = new KeycloakStoreClient(fixture.ServerAddress, new AuthenticationHeaderValue("Bearer", "invalid-fixture-token"), cache, TimeProvider.System, resource: "unauthorized-fixture");
        var error = await Should.ThrowAsync<ApiException>(() => unauthorized.Store.GetAsync(fixture.OrganizationId, cancellation)).ConfigureAwait(true);
        error.ResponseStatusCode.ShouldBe((int)HttpStatusCode.Unauthorized);
        _ = await Should.ThrowAsync<ApiException>(() => unauthorized.Store.GetAsync(fixture.OrganizationId, cancellation)).ConfigureAwait(true);
        unauthorized.Handler.Count.ShouldBe(2);
    }

    /// <summary>A same-ID entry in another realm cannot satisfy tenant resolution.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task RealmNamespacesNeverSharePositiveEntries()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var provider = MemoryCacheProvider();
        var cache = provider.GetRequiredService<IFusionCache>();
        var authorization = await fixture.AdminAuthorizationAsync(cancellation).ConfigureAwait(true);
        using var client = new KeycloakStoreClient(fixture.ServerAddress, authorization, cache, TimeProvider.System);
        _ = (await client.Store.GetAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true)).ShouldNotBeNull();
        using var other = new KeycloakStoreClient(fixture.ServerAddress, authorization, cache, TimeProvider.System, realm: "missing-fixture-realm");
        (await other.Store.GetAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true)).ShouldBeNull();
        other.Handler.Count.ShouldBe(1);
    }

    /// <summary>Invalid identifiers, writes and enumeration never reach Keycloak; cancellation remains honored.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task StoreRemainsReadOnlyBoundedAndCancellable()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var provider = MemoryCacheProvider();
        using var client = new KeycloakStoreClient(fixture.ServerAddress, await fixture.AdminAuthorizationAsync(cancellation).ConfigureAwait(true), provider.GetRequiredService<IFusionCache>(), TimeProvider.System);
        (await client.Store.GetAsync("../master", cancellation).ConfigureAwait(true)).ShouldBeNull();
        (await client.Store.GetByIdentifierAsync("fixture").ConfigureAwait(true)).ShouldBeNull();
        var tenant = new KeycloakTenantInfo { Id = fixture.OrganizationId, Identifier = fixture.OrganizationId, Name = "Forbidden mutation" };
        _ = await Should.ThrowAsync<NotSupportedException>(() => client.Store.AddAsync(tenant)).ConfigureAwait(true);
        _ = await Should.ThrowAsync<NotSupportedException>(() => client.Store.UpdateAsync(tenant)).ConfigureAwait(true);
        _ = await Should.ThrowAsync<NotSupportedException>(() => client.Store.RemoveAsync(fixture.OrganizationId)).ConfigureAwait(true);
        _ = await Should.ThrowAsync<NotSupportedException>(client.Store.GetAllAsync).ConfigureAwait(true);
        _ = await Should.ThrowAsync<NotSupportedException>(() => client.Store.GetAllAsync(10, 0)).ConfigureAwait(true);
        client.Handler.Count.ShouldBe(0);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);
        _ = await Should.ThrowAsync<OperationCanceledException>(() => client.Store.GetAsync(fixture.OrganizationId, cancelled.Token)).ConfigureAwait(true);
    }
}
