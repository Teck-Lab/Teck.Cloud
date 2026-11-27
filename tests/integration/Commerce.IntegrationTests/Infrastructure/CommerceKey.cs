using System.Security.Cryptography;
using Standard.Licensing.Security.Cryptography;

namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>Test-only EC signing material matching Standard.Licensing's encrypted PKCS#8 contract.</summary>
/// <param name="KeyId">The key identifier published in the ring.</param>
/// <param name="PublicKey">The public key in Standard.Licensing's string form.</param>
/// <param name="PrivateKey">The encrypted private key.</param>
/// <param name="PassPhrase">The pass phrase protecting the private key.</param>
internal sealed record CommerceKey(string KeyId, string PublicKey, string PrivateKey, string PassPhrase)
{
    internal static CommerceKey Generate(string keyId)
    {
        var pair = KeyGenerator.Create().GenerateKeyPair();
        var passPhrase = "commerce-fixture-" + keyId + "-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        return new CommerceKey(
            keyId,
            pair.ToPublicKeyString(),
            pair.ToEncryptedPrivateKeyString(passPhrase),
            passPhrase);
    }

    internal (string Key, string Value)[] PublicOnly(string index) =>
    [
        ("CommerceLicensing:KeyRing:" + index + ":KeyId", KeyId),
        ("CommerceLicensing:KeyRing:" + index + ":PublicKey", PublicKey),
    ];

    internal (string Key, string Value)[] Signing(string index) =>
    [
        ("CommerceLicensing:KeyRing:" + index + ":KeyId", KeyId),
        ("CommerceLicensing:KeyRing:" + index + ":PublicKey", PublicKey),
        ("CommerceLicensing:KeyRing:" + index + ":PrivateKey", PrivateKey),
        ("CommerceLicensing:KeyRing:" + index + ":PassPhrase", PassPhrase),
    ];
}
