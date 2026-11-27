using Keycloak.AuthServices.Sdk.Kiota;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Infrastructure.Auth;
using SharedKernel.Infrastructure.MultiTenancy;
using SharedKernel.Persistence.Caching;
using SharedKernel.Persistence.Database;
using Teck.Cloud.Commerce.Contracts;

namespace Teck.Cloud.Commerce.Gateway.Composition;

/// <summary>Composes the shared Keycloak-backed tenant directory for an edge host.</summary>
public static class CommerceGatewayDirectoryExtensions
{
    /// <summary>Registers the read-through directory with bounded five-second authorization freshness.</summary>
    /// <param name="services">Host registrations.</param>
    /// <param name="directory">Validated directory settings.</param>
    /// <param name="auth">The same trusted realm settings used for edge authentication.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCommerceGatewayDirectory(this IServiceCollection services, CommerceDirectoryOptions directory, CommerceAuthOptions auth)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(auth);
        _ = services.AddPersistenceFoundation();
        _ = directory.StandaloneRedis
            ? services.AddFoundationStandaloneRedis(directory.RedisConnectionString)
            : services.AddFoundationRedis(directory.RedisConnectionString);
        _ = services.AddFoundationKeycloakAdmin(
            new KeycloakAdminClientOptions
            {
                AuthServerUrl = auth.ServerUrl.ToString(),
                Realm = auth.Realm,
                Resource = directory.ReaderClientId,
                Credentials = new() { Secret = directory.ReaderSecret },
            },
            auth.Transport);
        _ = services.AddKeycloakTenantStore();
        return services;
    }
}
