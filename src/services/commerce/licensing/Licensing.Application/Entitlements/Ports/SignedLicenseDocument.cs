namespace Teck.Cloud.Commerce.Licensing.Application.Entitlements.Ports;

/// <summary>A signed Standard.Licensing document and the metadata needed for its envelope.</summary>
/// <param name="Document">The signed XML document.</param>
/// <param name="KeyId">The opaque signing key identifier.</param>
/// <param name="LicenseRevision">The monotonic signed-license revision.</param>
/// <param name="ExpiresAt">The exact signed UTC expiry.</param>
public sealed record SignedLicenseDocument(string Document, string KeyId, uint LicenseRevision, DateTimeOffset ExpiresAt);
