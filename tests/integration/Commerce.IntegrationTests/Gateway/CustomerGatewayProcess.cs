using Commerce.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.CustomerGateway.Host;

namespace Commerce.IntegrationTests.Gateway;

/// <summary>Starts the real customer gateway process on an ephemeral Kestrel port.</summary>
internal sealed class CustomerGatewayProcess : IAsyncDisposable
{
    private readonly WebApplication _application;
    private readonly SocketsHttpHandler _handler;

    private CustomerGatewayProcess(WebApplication application, Uri address)
    {
        _application = application;
        Address = address;
        _handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        Client = new HttpClient(_handler, disposeHandler: false) { BaseAddress = address, Timeout = TimeSpan.FromSeconds(30) };
    }

    internal Uri Address { get; }

    internal HttpClient Client { get; }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        _handler.Dispose();
        try
        {
            await _application.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            await _application.DisposeAsync().ConfigureAwait(false);
        }
    }

    internal static async Task<CustomerGatewayProcess> StartAsync(
        FakeCommerceBackend backend,
        CommerceKeycloakFixture keycloak,
        CancellationToken cancellationToken)
    {
        var arguments = Settings(backend, keycloak);
        var application = CustomerGatewayHost.Build(arguments, builder =>
        {
            _ = builder.Services.RemoveAll<ITenantDirectory>();
            _ = builder.Services.AddSingleton<ITenantDirectory>(new ActiveTenantDirectory(keycloak.CustomerA.TenantId));
        });
        try
        {
            await application.StartAsync(cancellationToken).ConfigureAwait(false);
            return new CustomerGatewayProcess(application, BoundAddress(application));
        }
        catch
        {
            await application.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static Uri BoundAddress(WebApplication application)
    {
        var feature = application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The customer gateway exposes no bound address.");
        return new Uri(feature.Addresses.Single());
    }

    private static string[] Settings(FakeCommerceBackend backend, CommerceKeycloakFixture keycloak)
    {
        var segments = keycloak.TokenEndpoint.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var realmIndex = Array.FindIndex(segments, segment => string.Equals(segment, "realms", StringComparison.Ordinal));
        var realm = realmIndex >= 0 && realmIndex + 1 < segments.Length ? segments[realmIndex + 1] : "teck-local";
        var server = keycloak.TokenEndpoint.GetLeftPart(UriPartial.Authority);
        return
        [
            "--urls=http://127.0.0.1:0",
            "--Commerce:Orders:Address=" + backend.Address,
            "--Commerce:Orders:Audience=commerce-orders",
            "--Commerce:Licensing:Address=" + backend.Address,
            "--Commerce:Licensing:Audience=commerce-licensing",
            "--Commerce:Transport=IsolatedDevelopment",
            "--Commerce:Directory:RedisConnectionString=127.0.0.1:1",
            "--Commerce:Directory:StandaloneRedis=true",
            "--Commerce:Directory:ReaderClientId=tenant-reader",
            "--Commerce:Directory:ReaderSecret=" + Guid.NewGuid().ToString("N"),
            "--Commerce:Identity:ServerUrl=" + server,
            "--Commerce:Identity:Realm=" + realm,
            "--Commerce:Identity:PublicIssuer=" + keycloak.Issuer.AbsoluteUri.TrimEnd('/'),
            "--Commerce:Identity:TokenEndpoint=" + keycloak.TokenEndpoint,
            "--Commerce:Identity:Transport=IsolatedDevelopment",
            "--Commerce:Identity:ClientId=" + keycloak.CustomerGatewayClientId,
            "--Commerce:Identity:ClientSecret=" + keycloak.CustomerGatewayClientSecret,
        ];
    }
}
