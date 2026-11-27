namespace Teck.Cloud.Commerce.Licensing.Application.Entitlements.ReadModels;

/// <summary>The current active signed license envelope returned to an authorized caller.</summary>
/// <param name="Document">The signed document; opaque to clients.</param>
/// <param name="KeyId">The opaque signing key identifier.</param>
/// <param name="Revision">The signed-license revision, distinct from the desired generation.</param>
/// <param name="ExpiresAt">The exact UTC expiry.</param>
public sealed record LicenseEnvelopeView(string Document, string KeyId, uint Revision, DateTimeOffset ExpiresAt);
