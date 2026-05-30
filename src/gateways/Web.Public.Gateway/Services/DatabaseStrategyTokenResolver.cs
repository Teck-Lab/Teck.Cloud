using System.Security.Claims;
using SharedKernel.Core.Pricing;

namespace Web.Public.Gateway.Services;

/// <summary>
/// Resolves the tenant database strategy from JWT claims.
/// </summary>
internal interface IDatabaseStrategyTokenResolver
{
    /// <summary>
    /// Attempts to read the database strategy from the JWT claims.
    /// Returns null if the claim is missing or the value is not a valid strategy.
    /// </summary>
    /// <param name="user">The authenticated user principal.</param>
    /// <param name="claimName">The claim name containing the database strategy.</param>
    /// <returns>The database strategy name, or null if not found/invalid.</returns>
    string? ResolveStrategyFromClaims(ClaimsPrincipal user, string claimName);
}

/// <summary>
/// Default implementation that reads and validates the database strategy from JWT claims.
/// </summary>
internal sealed class DatabaseStrategyTokenResolver : IDatabaseStrategyTokenResolver
{
    /// <inheritdoc />
    public string? ResolveStrategyFromClaims(ClaimsPrincipal user, string claimName)
    {
        ArgumentNullException.ThrowIfNull(user);

        string? claimValue = user.FindFirst(claimName)?.Value;
        if (string.IsNullOrWhiteSpace(claimValue))
        {
            return null;
        }

        string normalized = claimValue.Trim();

        // Validate it's a known strategy using the SmartEnum
        if (DatabaseStrategy.TryFromName(normalized, ignoreCase: true, out _))
        {
            return normalized;
        }

        return null;
    }
}
