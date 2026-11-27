namespace Teck.Cloud.Commerce.Licensing.Infrastructure.Security;

/// <summary>One public verification key and, only for the active signer, its secret material.</summary>
public sealed class LicensingKeyRingEntry
{
    /// <summary>Gets the opaque rotation identifier.</summary>
    public string KeyId { get; init; } = string.Empty;

    /// <summary>Gets the base64 SubjectPublicKeyInfo public verification key.</summary>
    public string PublicKey { get; init; } = string.Empty;

    /// <summary>Gets the base64 encrypted private key; permitted only for the active signer.</summary>
    public string? PrivateKey { get; init; }

    /// <summary>Gets the private-key pass phrase; permitted only for the active signer.</summary>
    public string? PassPhrase { get; init; }
}
