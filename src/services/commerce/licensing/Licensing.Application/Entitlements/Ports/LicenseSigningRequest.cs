using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGrants;

namespace Teck.Cloud.Commerce.Licensing.Application.Entitlements.Ports;

/// <summary>The complete signed payload requested for one accepted generation.</summary>
/// <param name="TenantId">The immutable organization UUID.</param>
/// <param name="OrderId">The bound order.</param>
/// <param name="SubscriptionId">The bound subscription.</param>
/// <param name="Tier">The effective database tier.</param>
/// <param name="AddOnCodes">The canonical permitted add-on codes.</param>
/// <param name="LicenseRevision">The monotonic signed-license revision.</param>
/// <param name="KeyId">The active opaque signing key identifier.</param>
/// <param name="ExpiresAt">The exact UTC expiry already clamped by Orders.</param>
public sealed record LicenseSigningRequest(
    string TenantId,
    Guid OrderId,
    Guid SubscriptionId,
    LicenseTier Tier,
    IReadOnlyList<string> AddOnCodes,
    uint LicenseRevision,
    string KeyId,
    DateTimeOffset ExpiresAt);
