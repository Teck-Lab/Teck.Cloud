using Microsoft.Extensions.Configuration;

namespace Teck.Cloud.Commerce.Licensing.Infrastructure.Security;

/// <summary>Injected managed-secret signing configuration read from the service environment.</summary>
public sealed class LicensingKeyRingOptions
{
    /// <summary>The configuration section path.</summary>
    public const string SectionName = "CommerceLicensing";

    private LicensingKeyRingOptions(string activeKeyId, IReadOnlyList<LicensingKeyRingEntry> entries)
    {
        ActiveKeyId = activeKeyId;
        Entries = entries;
    }

    /// <summary>Gets the active signer identifier.</summary>
    public string ActiveKeyId { get; }

    /// <summary>Gets the configured verification ring.</summary>
    public IReadOnlyList<LicensingKeyRingEntry> Entries { get; }

    /// <summary>Reads and shape-validates the key ring from managed secret configuration.</summary>
    /// <param name="section">The <c lang="text">CommerceLicensing</c> section.</param>
    /// <returns>The immutable key-ring configuration.</returns>
    /// <exception cref="InvalidOperationException">The active key or ring shape is invalid.</exception>
    public static LicensingKeyRingOptions FromConfiguration(IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        var activeKeyId = section["ActiveKeyId"];
        if (string.IsNullOrWhiteSpace(activeKeyId))
        {
            throw new InvalidOperationException("CommerceLicensing:ActiveKeyId is required.");
        }

        var entries = section.GetSection("KeyRing").GetChildren()
            .Select(child => new LicensingKeyRingEntry
            {
                KeyId = child["KeyId"] ?? string.Empty,
                PublicKey = child["PublicKey"] ?? string.Empty,
                PrivateKey = child["PrivateKey"],
                PassPhrase = child["PassPhrase"],
            })
            .ToArray();
        return entries.Length > 0
            ? new LicensingKeyRingOptions(activeKeyId, entries)
            : throw new InvalidOperationException("CommerceLicensing:KeyRing must contain at least one verification key.");
    }
}
