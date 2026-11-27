namespace Teck.Cloud.Commerce.Licensing.Application.Entitlements.Ports;

/// <summary>A parsed license document, verified only when the status is valid.</summary>
/// <param name="Status">The verification outcome.</param>
/// <param name="Claims">Verified claims; null unless <paramref name="Status"/> is valid.</param>
public sealed record LicenseDocumentInspection(LicenseDocumentStatus Status, LicenseDocumentClaims? Claims);
