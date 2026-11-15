using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SharedKernel.Infrastructure.Auth;

/// <summary>Composes endpoint-specific assurance checks with native JWT authentication.</summary>
public static class KeycloakAssuranceExtensions
{
    /// <summary>Registers the bounded assurance authorization handler and system clock.</summary>
    /// <param name="services">Host registrations.</param>
    /// <returns>The existing collection for additional registration.</returns>
    public static IServiceCollection AddKeycloakAssurance(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, KeycloakAssuranceHandler>());
        return services;
    }

    /// <summary>Requires a trusted realm assurance value and recent interactive authentication.</summary>
    /// <param name="policy">The host's explicit endpoint policy.</param>
    /// <param name="requiredAcr">The exact native ACR expected from the realm flow.</param>
    /// <param name="maximumAuthenticationAge">Maximum elapsed time since authentication.</param>
    /// <returns>The policy builder.</returns>
    public static AuthorizationPolicyBuilder RequireKeycloakAssurance(this AuthorizationPolicyBuilder policy, string requiredAcr, TimeSpan maximumAuthenticationAge)
        => policy.RequireAuthenticatedUser().AddRequirements(new KeycloakAssuranceRequirement(requiredAcr, maximumAuthenticationAge));
}
