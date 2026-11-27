namespace Teck.Cloud.Commerce.Licensing.Application.Entitlements.ReadModels;

/// <summary>Verification result: effective claims when valid, or one stable rejection.</summary>
/// <param name="Valid">Whether the document is effective.</param>
/// <param name="Claims">Effective claims; present only when valid.</param>
/// <param name="Rejection">The stable rejection; none when valid.</param>
public sealed record LicenseVerificationView(bool Valid, LicenseClaimView? Claims, LicenseRejection Rejection);
