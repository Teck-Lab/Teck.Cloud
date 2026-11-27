namespace Teck.Cloud.Commerce.Gateway.Models;

/// <summary>The signed license envelope returned only for an active own order.</summary>
/// <param name="Document">Opaque signed document; never parsed by the gateway.</param>
/// <param name="KeyId">Opaque signing key identifier.</param>
/// <param name="Revision">Signed license revision.</param>
/// <param name="ExpiresAt">Clamped UTC expiry.</param>
public sealed record LicenseView(string Document, string KeyId, uint Revision, string ExpiresAt);
