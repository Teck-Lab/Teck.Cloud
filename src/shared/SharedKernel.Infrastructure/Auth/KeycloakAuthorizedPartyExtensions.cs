using System.Collections.Frozen;
using Microsoft.AspNetCore.Authorization;

namespace SharedKernel.Infrastructure.Auth;

/// <summary>Restricts an already validated issuer principal to explicitly trusted token-issuing clients.</summary>
public static class KeycloakAuthorizedPartyExtensions
{
    /// <summary>Requires exactly one authorized-party claim on one authenticated identity.</summary>
    /// <param name="policy">A host policy composed with native JWT validation and resource authorization.</param>
    /// <param name="allowedClients">Server-owned exact client IDs; never request input.</param>
    /// <returns>The existing policy builder.</returns>
    /// <exception cref="ArgumentException">The allowlist is empty or contains a blank client ID.</exception>
    public static AuthorizationPolicyBuilder RequireKeycloakAuthorizedParty(this AuthorizationPolicyBuilder policy, params string[] allowedClients)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(allowedClients);
        if (allowedClients.Length == 0 || allowedClients.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one nonblank trusted client ID is required.", nameof(allowedClients));
        }

        var allowed = allowedClients.ToFrozenSet(StringComparer.Ordinal);
        return policy.RequireAuthenticatedUser().RequireAssertion(context =>
        {
            var identities = context.User.Identities.Where(identity => identity.IsAuthenticated).Take(2).ToArray();
            var claims = context.User.FindAll("azp").Take(2).ToArray();
            var accepted = identities.Length == 1 && claims.Length == 1
                && identities[0].HasClaim(claims[0].Type, claims[0].Value)
                && allowed.Contains(claims[0].Value);
            if (!accepted)
            {
                context.Fail();
            }

            return accepted;
        });
    }
}
