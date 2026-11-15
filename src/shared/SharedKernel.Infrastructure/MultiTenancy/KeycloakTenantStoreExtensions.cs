using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SharedKernel.Core.MultiTenancy;

namespace SharedKernel.Infrastructure.MultiTenancy;

/// <summary>One store instance for Finbuckle, request validation and Wolverine message validation.</summary>
public static class KeycloakTenantStoreExtensions
{
    /// <summary>Selects the read-through store after Keycloak Admin and FusionCache setup, replacing earlier tenant-directory bindings.</summary>
    /// <param name="services">Host registrations.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddKeycloakTenantStore(this IServiceCollection services)
    {
        services.TryAddScoped<KeycloakTenantStore>();
        _ = services.RemoveAll<IMultiTenantStore<KeycloakTenantInfo>>();
        _ = services.RemoveAll<ITenantDirectory>();
        _ = services.AddScoped<IMultiTenantStore<KeycloakTenantInfo>>(provider => provider.GetRequiredService<KeycloakTenantStore>());
        _ = services.AddScoped<ITenantDirectory>(provider => provider.GetRequiredService<KeycloakTenantStore>());
        return services;
    }
}
