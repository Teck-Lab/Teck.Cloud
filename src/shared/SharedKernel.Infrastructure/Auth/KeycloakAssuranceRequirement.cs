using Microsoft.AspNetCore.Authorization;

namespace SharedKernel.Infrastructure.Auth;

/// <summary>Requires an exact signed Keycloak ACR and a bounded authentication age.</summary>
public sealed class KeycloakAssuranceRequirement : IAuthorizationRequirement
{
    /// <summary>Initializes a new instance of the <see cref="KeycloakAssuranceRequirement"/> class.</summary>
    /// <param name="requiredAcr">The assurance value defined by the trusted realm flow.</param>
    /// <param name="maximumAuthenticationAge">Maximum elapsed time since interactive authentication.</param>
    public KeycloakAssuranceRequirement(string requiredAcr, TimeSpan maximumAuthenticationAge)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredAcr);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumAuthenticationAge, TimeSpan.Zero);
        RequiredAcr = requiredAcr;
        MaximumAuthenticationAge = maximumAuthenticationAge;
    }

    /// <summary>Gets the exact assurance value required by this endpoint.</summary>
    public string RequiredAcr { get; }

    /// <summary>Gets the maximum age of the original authentication, independent of token refresh.</summary>
    public TimeSpan MaximumAuthenticationAge { get; }
}
