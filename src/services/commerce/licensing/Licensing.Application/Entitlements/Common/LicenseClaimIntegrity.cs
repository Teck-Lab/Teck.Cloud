using SharedKernel.Core.Database;
using Teck.Cloud.Commerce.Licensing.Application.Entitlements.Ports;
using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGenerations;
using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGrants;

namespace Teck.Cloud.Commerce.Licensing.Application.Entitlements.Common;

/// <summary>Cross-checks verified document claims against a durable grant row.</summary>
internal static class LicenseClaimIntegrity
{
    /// <summary>Returns true when the signed claims are exactly the durable grant snapshot.</summary>
    /// <param name="claims">The verified signed document claims.</param>
    /// <param name="grant">The durable grant row the document must match.</param>
    /// <returns>True when every claimed field matches the stored grant.</returns>
    internal static bool Matches(LicenseDocumentClaims claims, LicenseGrant grant) =>
        claims is not null
        && grant.Document is not null
        && string.Equals(claims.TenantId, grant.TenantId, StringComparison.Ordinal)
        && claims.OrderId == grant.OrderId
        && claims.SubscriptionId == grant.SubscriptionId
        && claims.Tier == grant.Tier
        && claims.LicenseRevision == grant.LicenseRevision
        && string.Equals(claims.KeyId, grant.KeyId, StringComparison.Ordinal)
        && claims.ExpiresAt.ToUniversalTime() == grant.ExpiresAt?.ToUniversalTime()
        && claims.AddOnCodes.SequenceEqual(grant.AddOnCodes, StringComparer.Ordinal);

    /// <summary>Truncates a UTC instant to PostgreSQL timestamp precision.</summary>
    /// <param name="instant">The instant to truncate.</param>
    /// <returns>The instant truncated to whole microseconds in UTC.</returns>
    internal static DateTimeOffset ToMicroseconds(DateTimeOffset instant) =>
        new(instant.UtcTicks - (instant.UtcTicks % 10), TimeSpan.Zero);

    /// <summary>Finds the current active grant for a generation, or null when absent or superseded.</summary>
    /// <param name="grants">The untracked grant repository.</param>
    /// <param name="generation">The exact generation fence to match.</param>
    /// <param name="utcNow">The application UTC instant that must precede expiry.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The usable active grant, or null when absent, superseded or expired.</returns>
    internal static async Task<LicenseGrant?> CurrentActiveAsync(
        IGenericReadRepository<LicenseGrant, Guid> grants,
        LicenseGeneration generation,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        var grant = await grants.FindOneAsync(
            candidate => candidate.TenantId == generation.TenantId
                && candidate.OrderId == generation.OrderId
                && candidate.SubscriptionId == generation.SubscriptionId
                && candidate.DesiredRevision == generation.DesiredRevision
                && candidate.LicenseRevision == generation.LicenseRevision
                && candidate.State == LicenseGrantState.Active,
            cancellationToken).ConfigureAwait(false);
        return grant?.IsUsable(generation.DesiredRevision, utcNow) is true ? grant : null;
    }
}
