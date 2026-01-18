using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Secrets;

/// <summary>
/// Extension methods for registering Vault secrets management.
/// </summary>
public static class VaultServiceExtensions
{
    /// <summary>
    /// Adds HashiCorp Vault secrets management to the service collection.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddVaultSecretsManagement(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<VaultOptions>(configuration.GetSection(VaultOptions.SectionName));
        services.AddMemoryCache();
        services.AddSingleton<IVaultSecretsManager, VaultSecretsManager>();

        return services;
    }

    /// <summary>
    /// Adds HashiCorp Vault secrets management with custom configuration.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddVaultSecretsManagement(
        this IServiceCollection services,
        Action<VaultOptions> configure)
    {
        services.Configure(configure);
        services.AddMemoryCache();
        services.AddSingleton<IVaultSecretsManager, VaultSecretsManager>();

        return services;
    }
}
