namespace Teck.Cloud.Commerce.Licensing.Application.Entitlements.Ports;

/// <summary>Outcome of parsing and cryptographically verifying a license document.</summary>
public enum LicenseDocumentStatus
{
    /// <summary>The signature verifies against a trusted key.</summary>
    Valid = 0,

    /// <summary>The document is well formed but its signature does not verify.</summary>
    InvalidSignature = 1,

    /// <summary>The key identifier is not in the trusted public ring.</summary>
    UnknownKey = 2,

    /// <summary>The document is not a bounded, well-formed license document.</summary>
    Malformed = 3,
}
