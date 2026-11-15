using Duende.AccessTokenManagement;
using Microsoft.Extensions.DependencyInjection;
using AdminOptions = Keycloak.AuthServices.Sdk.Kiota.KeycloakAdminClientOptions;

namespace SharedKernel.Infrastructure.Auth;

/// <summary>Registers the upstream Admin SDKs with service-account token lifecycle management.</summary>
public static class KeycloakAdminExtensions
{
    private const string TokenClientName = "teck-keycloak-admin";

    /// <summary>Adds Kiota and hand-written Admin clients for one explicitly authorized realm-local service account.</summary>
    /// <param name="services">Host registrations.</param>
    /// <param name="options">Trusted server, realm and secret-supplied service-account configuration.</param>
    /// <param name="transport">The explicit transport boundary; HTTPS by default.</param>
    /// <returns>The Kiota HTTP client builder.</returns>
    public static IHttpClientBuilder AddFoundationKeycloakAdmin(this IServiceCollection services, AdminOptions options, IdentityTransport transport = IdentityTransport.Https)
    {
        ArgumentNullException.ThrowIfNull(options);
        var endpoint = Validate(options, transport);
        _ = services.AddClientCredentialsTokenManagement().AddClient(TokenClientName, client =>
        {
            client.ClientId = ClientId.Parse(options.Resource);
            client.ClientSecret = ClientSecret.Parse(options.Credentials.Secret);
            client.TokenEndpoint = endpoint;
            client.Scope = Scope.Parse("kc-admin-api");
        });
        _ = services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(CreateHandler);
        var name = ClientCredentialsClientName.Parse(TokenClientName);
        var sdkOptions = new Keycloak.AuthServices.Sdk.KeycloakAdminClientOptions
        {
            AuthServerUrl = options.AuthServerUrl,
            Realm = options.Realm,
            Resource = options.Resource,
            Credentials = options.Credentials,
        };
        _ = Keycloak.AuthServices.Sdk.ServiceCollectionExtensions.AddKeycloakAdminHttpClient(services, sdkOptions, ConfigureClient)
            .ConfigurePrimaryHttpMessageHandler(CreateHandler)
            .AddClientCredentialsTokenHandler(name);
        return Keycloak.AuthServices.Sdk.Kiota.ServiceCollectionExtensions.AddKeycloakAdminHttpClient(services, options, ConfigureClient)
            .ConfigurePrimaryHttpMessageHandler(CreateHandler)
            .AddClientCredentialsTokenHandler(name);
    }

    private static void ConfigureClient(HttpClient client) => client.Timeout = TimeSpan.FromSeconds(10);

    private static SocketsHttpHandler CreateHandler() => new() { AllowAutoRedirect = false, UseCookies = false };

    private static Uri Validate(AdminOptions options, IdentityTransport transport)
    {
        if (string.IsNullOrWhiteSpace(options.Realm) || string.IsNullOrWhiteSpace(options.Resource)
            || string.IsNullOrWhiteSpace(options.Credentials.Secret) || options.Realm.IndexOfAny(['/', '?', '#']) >= 0
            || !Uri.TryCreate(options.AuthServerUrl, UriKind.Absolute, out var server) || server.Query.Length != 0)
        {
            throw new InvalidOperationException("A trusted realm-local Admin service account and server are required.");
        }

        IdentityTransportPolicy.Validate(server, transport);
        var endpoint = new Uri(options.KeycloakTokenEndpoint);
        IdentityTransportPolicy.Validate(endpoint, transport);
        return endpoint;
    }
}
