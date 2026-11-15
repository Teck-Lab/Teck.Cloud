using System.Globalization;
using Microsoft.AspNetCore.Authorization;

namespace SharedKernel.Infrastructure.Auth;

/// <summary>Checks assurance only after the normal token authentication boundary has succeeded.</summary>
/// <param name="timeProvider">Trusted host clock used for authentication freshness.</param>
public sealed class KeycloakAssuranceHandler(TimeProvider timeProvider) : AuthorizationHandler<KeycloakAssuranceRequirement>
{
    /// <inheritdoc/>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, KeycloakAssuranceRequirement requirement)
    {
        var identities = context.User.Identities.Where(identity => identity.IsAuthenticated).ToArray();
        if (identities.Length != 1)
        {
            context.Fail();
            return Task.CompletedTask;
        }

        var identity = identities[0];
        var assurance = identity.FindAll("acr").ToArray();
        var authenticationTimes = identity.FindAll("auth_time").ToArray();
        var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        if (assurance.Length == 1 && authenticationTimes.Length == 1
            && string.Equals(assurance[0].Value, requirement.RequiredAcr, StringComparison.Ordinal)
            && long.TryParse(authenticationTimes[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var authenticatedAt)
            && authenticatedAt >= 0 && authenticatedAt <= now
            && now - authenticatedAt <= requirement.MaximumAuthenticationAge.TotalSeconds)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }

        return Task.CompletedTask;
    }
}
