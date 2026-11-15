using System.Net;
using System.Text;
using Keycloak.AuthServices.Sdk.Kiota;
using Keycloak.AuthServices.Sdk.Kiota.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using SharedKernel.Infrastructure.MultiTenancy;
using SharedKernel.Persistence.Caching;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace SharedKernel.Infrastructure.UnitTests;

/// <summary>Checks provider response validation through the real SDK parser and cache boundary.</summary>
public sealed class KeycloakTenantResponseTests
{
    private const string TenantId = "391bd9ef-e121-4a58-9c48-68b8c60b00ee";
    private const string Active = "{\"id\":\"391bd9ef-e121-4a58-9c48-68b8c60b00ee\",\"enabled\":true,\"name\":\"Fixture\"}";

    /// <summary>Malformed successful responses are provider faults and cannot become cached absence.</summary>
    /// <param name="body">The actual HTTP response body.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"id\":\"391bd9ef-e121-4a58-9c48-68b8c60b00ee\"}")]
    [InlineData("{\"enabled\":false}")]
    [InlineData("{\"id\":\"e9103886-ddd4-4fb1-97f9-89fb4bdc7139\",\"enabled\":false}")]
    [InlineData("{\"id\":\"e9103886-ddd4-4fb1-97f9-89fb4bdc7139\",\"enabled\":true}")]
    public async Task MalformedRepresentationsFailAndAreNotCached(string body)
    {
        using var handler = new OrganizationHandler { Body = body };
        using var http = new HttpClient(handler);
        using var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: http) { BaseUrl = "https://identity.test" };
        await using var services = CacheProvider();
        var store = CreateStore(adapter, services);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => store.GetAsync(TenantId, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        handler.Body = Active;
        (await store.GetAsync(TenantId, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldNotBeNull().Id.ShouldBe(TenantId);
        handler.Count.ShouldBe(2);
    }

    /// <summary>Only authoritative not-found and a well-formed disabled organization are cached negatives.</summary>
    /// <param name="notFound">Whether the provider reports absence rather than disabled state.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthoritativeNegativeResponsesRemainCached(bool notFound)
    {
        using var handler = new OrganizationHandler
        {
            Status = notFound ? HttpStatusCode.NotFound : HttpStatusCode.OK,
            Body = notFound ? "{}" : "{\"id\":\"391bd9ef-e121-4a58-9c48-68b8c60b00ee\",\"enabled\":false}",
        };
        using var http = new HttpClient(handler);
        using var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: http) { BaseUrl = "https://identity.test" };
        await using var services = CacheProvider();
        var store = CreateStore(adapter, services);
        (await store.GetAsync(TenantId, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeNull();
        handler.Body = Active;
        handler.Status = HttpStatusCode.OK;
        (await store.GetAsync(TenantId, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeNull();
        handler.Count.ShouldBe(1);
    }

    private static ServiceProvider CacheProvider()
    {
        var services = new ServiceCollection();
        _ = services.AddFoundationCache();
        return services.BuildServiceProvider();
    }

    private static KeycloakTenantStore CreateStore(HttpClientRequestAdapter adapter, IServiceProvider services) => new(
        new KeycloakAdminApiClient(adapter),
        services.GetRequiredService<IFusionCache>(),
        Options.Create(new KeycloakAdminClientOptions { AuthServerUrl = "https://identity.test", Realm = "teck", Resource = "tenant-reader" }));

    private sealed class OrganizationHandler : HttpMessageHandler
    {
        internal string Body { get; set; } = Active;

        internal HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        internal int Count { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Count++;
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Body, Encoding.UTF8, "application/json") });
        }
    }
}
