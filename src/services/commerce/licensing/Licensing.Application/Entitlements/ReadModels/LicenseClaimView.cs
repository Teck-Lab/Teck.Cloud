using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGrants;

namespace Teck.Cloud.Commerce.Licensing.Application.Entitlements.ReadModels;

/// <summary>Verified effective license claims returned by the verification API.</summary>
/// <param name="TenantId">The signed organization UUID.</param>
/// <param name="OrderId">The signed order.</param>
/// <param name="SubscriptionId">The signed subscription.</param>
/// <param name="Tier">The signed database tier.</param>
/// <param name="AddOnCodes">The signed canonical add-on codes.</param>
/// <param name="Revision">The signed license revision.</param>
/// <param name="KeyId">The signed opaque key identifier.</param>
/// <param name="ExpiresAt">The signed exact UTC expiry.</param>
public sealed record LicenseClaimView(
    string TenantId,
    Guid OrderId,
    Guid SubscriptionId,
    LicenseTier Tier,
    IReadOnlyList<string> AddOnCodes,
    uint Revision,
    string KeyId,
    DateTimeOffset ExpiresAt);
