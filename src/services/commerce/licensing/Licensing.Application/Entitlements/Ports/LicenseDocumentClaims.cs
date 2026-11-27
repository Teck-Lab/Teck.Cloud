using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGrants;

namespace Teck.Cloud.Commerce.Licensing.Application.Entitlements.Ports;

/// <summary>Effective claims extracted from a signature-verified license document.</summary>
/// <param name="TenantId">The signed organization UUID.</param>
/// <param name="OrderId">The signed order.</param>
/// <param name="SubscriptionId">The signed subscription.</param>
/// <param name="Tier">The signed database tier.</param>
/// <param name="AddOnCodes">The signed canonical add-on codes.</param>
/// <param name="LicenseRevision">The signed license revision.</param>
/// <param name="KeyId">The signed opaque key identifier.</param>
/// <param name="ExpiresAt">The signed exact UTC expiry.</param>
public sealed record LicenseDocumentClaims(
    string TenantId,
    Guid OrderId,
    Guid SubscriptionId,
    LicenseTier Tier,
    IReadOnlyList<string> AddOnCodes,
    uint LicenseRevision,
    string KeyId,
    DateTimeOffset ExpiresAt);
