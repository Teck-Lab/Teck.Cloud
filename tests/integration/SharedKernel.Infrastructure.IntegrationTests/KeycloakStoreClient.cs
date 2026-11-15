using System.Net;
using System.Net.Http.Headers;
using Keycloak.AuthServices.Sdk.Kiota;
using Keycloak.AuthServices.Sdk.Kiota.Admin;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using SharedKernel.Infrastructure.MultiTenancy;
using ZiggyCreatures.Caching.Fusion;

namespace SharedKernel.Infrastructure.IntegrationTests;

internal sealed class KeycloakStoreClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly HttpClientRequestAdapter _adapter;

    internal KeycloakStoreClient(Uri address, AuthenticationHeaderValue authorization, IFusionCache cache, TimeProvider clock, string realm = "teck-local", string resource = "directory-fixture")
    {
        Handler = new CountedTransport { InnerHandler = new SocketsHttpHandler { AllowAutoRedirect = false } };
        _http = new HttpClient(Handler) { BaseAddress = address, Timeout = TimeSpan.FromSeconds(10) };
        _http.DefaultRequestHeaders.Authorization = authorization;
        _adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: _http) { BaseUrl = address.ToString().TrimEnd('/') };
        var client = new KeycloakAdminApiClient(_adapter);
        var options = new KeycloakAdminClientOptions { AuthServerUrl = address.ToString(), Realm = realm, Resource = resource };
        Store = new KeycloakTenantStore(client, cache, Options.Create(options), clock);
    }

    internal KeycloakTenantStore Store { get; }

    internal CountedTransport Handler { get; }

    public void Dispose()
    {
        _adapter.Dispose();
        _http.Dispose();
    }

    internal sealed class CountedTransport : DelegatingHandler
    {
        private int _count;

        internal int Count => Volatile.Read(ref _count);

        internal bool Unavailable { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _count);
            cancellationToken.ThrowIfCancellationRequested();
            return Unavailable
                ? Task.FromException<HttpResponseMessage>(new HttpRequestException("Injected identity transport outage.", inner: null, HttpStatusCode.ServiceUnavailable))
                : base.SendAsync(request, cancellationToken);
        }
    }
}
