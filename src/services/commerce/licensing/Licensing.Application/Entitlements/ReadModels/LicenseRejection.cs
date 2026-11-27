namespace Teck.Cloud.Commerce.Licensing.Application.Entitlements.ReadModels;

/// <summary>Stable license verification rejection codes.</summary>
public enum LicenseRejection
{
    /// <summary>The document is valid and effective.</summary>
    None = 0,

    /// <summary>The signed payload does not match its signature.</summary>
    InvalidSignature = 1,

    /// <summary>The signed expiry is not in the future.</summary>
    Expired = 2,

    /// <summary>The generation is revoked or superseded.</summary>
    Revoked = 3,

    /// <summary>The document does not bind the expected tenant or subscription.</summary>
    WrongBinding = 4,

    /// <summary>The signing key identifier is not trusted.</summary>
    UnknownKey = 5,
}
