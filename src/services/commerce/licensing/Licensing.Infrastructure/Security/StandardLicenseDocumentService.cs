using System.Globalization;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using ErrorOr;
using Standard.Licensing;
using Teck.Cloud.Commerce.Licensing.Application.Entitlements.Ports;
using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGrants;

namespace Teck.Cloud.Commerce.Licensing.Infrastructure.Security;

/// <summary>Standard.Licensing signing and verification over an injected managed-secret key ring.</summary>
public sealed class StandardLicenseDocumentService : ILicenseDocumentService
{
    private const int MaximumDocumentLength = 65_536;
    private const string TenantClaim = "tenant_id";
    private const string OrderClaim = "order_id";
    private const string SubscriptionClaim = "subscription_id";
    private const string TierClaim = "tier";
    private const string AddOnsClaim = "add_on_codes";
    private const string RevisionClaim = "revision";
    private const string KeyIdClaim = "key_id";
    private const string ExpiryClaim = "expires_at";

    private readonly Dictionary<string, string> _publicKeys;
    private readonly string _activePrivateKey;
    private readonly string _activePassPhrase;

    /// <summary>Initializes a new instance of the <see cref="StandardLicenseDocumentService"/> class and validates the active signer and the trusted public ring.</summary>
    /// <param name="options">The injected managed-secret key-ring configuration.</param>
    /// <exception cref="InvalidOperationException">The active signer or public ring is not usable.</exception>
    public StandardLicenseDocumentService(LicensingKeyRingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Entries.Any(entry => string.IsNullOrWhiteSpace(entry.KeyId) || entry.KeyId.Length > 64 || entry.KeyId.Any(char.IsControl)))
        {
            throw new InvalidOperationException("Every key-ring entry requires a bounded opaque identifier.");
        }

        if (options.Entries.GroupBy(entry => entry.KeyId, StringComparer.Ordinal).Any(group => group.Skip(1).Any()))
        {
            throw new InvalidOperationException("Key-ring identifiers must be unique.");
        }

        var active = options.Entries.SingleOrDefault(entry => string.Equals(entry.KeyId, options.ActiveKeyId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("The active signing key is not present in the configured ring.");
        if (string.IsNullOrWhiteSpace(active.PrivateKey) || string.IsNullOrWhiteSpace(active.PassPhrase))
        {
            throw new InvalidOperationException("The active signing key requires injected private key material.");
        }

        var activePrivateKey = active.PrivateKey;
        var activePassPhrase = active.PassPhrase;

        if (options.Entries.Any(entry => !string.Equals(entry.KeyId, active.KeyId, StringComparison.Ordinal)
            && (!string.IsNullOrEmpty(entry.PrivateKey) || !string.IsNullOrEmpty(entry.PassPhrase))))
        {
            throw new InvalidOperationException("Only the active signer may carry private key material.");
        }

        var publicKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in options.Entries)
        {
            if (!IsUsablePublicKey(entry.PublicKey))
            {
                throw new InvalidOperationException("Every key-ring entry requires a base64 EC SubjectPublicKeyInfo verification key.");
            }

            publicKeys.Add(entry.KeyId, entry.PublicKey);
        }

        _publicKeys = publicKeys;
        _activePrivateKey = activePrivateKey;
        _activePassPhrase = activePassPhrase;
        ActiveKeyId = active.KeyId;
        if (!CanSignWithActiveKey())
        {
            throw new InvalidOperationException("The active signing key material is not usable.");
        }
    }

    /// <inheritdoc/>
    public string ActiveKeyId { get; }

    /// <inheritdoc/>
    public ErrorOr<SignedLicenseDocument> Sign(LicenseSigningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Guid.TryParseExact(request.TenantId, "D", out _)
            || request.OrderId == Guid.Empty
            || request.SubscriptionId == Guid.Empty
            || request.LicenseRevision == 0
            || !string.Equals(request.KeyId, ActiveKeyId, StringComparison.Ordinal))
        {
            return Error.Validation("invalid_request", "A complete binding and the active signer are required.");
        }

        try
        {
            var expiresAt = request.ExpiresAt.ToUniversalTime();
            var license = License.New()
                .WithUniqueIdentifier(Guid.NewGuid())
                .As(LicenseType.Standard)
                .ExpiresAt(expiresAt.UtcDateTime)
                .WithAdditionalAttributes(attributes =>
                {
                    attributes.Add(TenantClaim, request.TenantId);
                    attributes.Add(OrderClaim, request.OrderId.ToString("D"));
                    attributes.Add(SubscriptionClaim, request.SubscriptionId.ToString("D"));
                    attributes.Add(TierClaim, ToWireTier(request.Tier));
                    attributes.Add(AddOnsClaim, string.Join(',', request.AddOnCodes));
                    attributes.Add(RevisionClaim, request.LicenseRevision.ToString(CultureInfo.InvariantCulture));
                    attributes.Add(KeyIdClaim, ActiveKeyId);
                    attributes.Add(ExpiryClaim, expiresAt.ToString("O", CultureInfo.InvariantCulture));
                })
                .CreateAndSignWithPrivateKey(_activePrivateKey, _activePassPhrase);
            return new SignedLicenseDocument(license.ToString(), ActiveKeyId, request.LicenseRevision, expiresAt);
        }
        catch (CryptographicException)
        {
            return SigningFailed();
        }
        catch (ArgumentException)
        {
            return SigningFailed();
        }
        catch (FormatException)
        {
            return SigningFailed();
        }
        catch (InvalidOperationException)
        {
            return SigningFailed();
        }
    }

    /// <inheritdoc/>
    public LicenseDocumentInspection Inspect(string document)
    {
        if (string.IsNullOrWhiteSpace(document) || document.Length > MaximumDocumentLength)
        {
            return Malformed();
        }

        if (!TryReadKeyId(document, out var keyId))
        {
            return Malformed();
        }

        if (string.IsNullOrEmpty(keyId) || !_publicKeys.TryGetValue(keyId, out var publicKey))
        {
            return string.IsNullOrEmpty(keyId)
                ? new LicenseDocumentInspection(LicenseDocumentStatus.InvalidSignature, Claims: null)
                : new LicenseDocumentInspection(LicenseDocumentStatus.UnknownKey, Claims: null);
        }

        if (!TryLoad(document, out var license) || license is null || !HasValidSignature(license, publicKey))
        {
            return new LicenseDocumentInspection(LicenseDocumentStatus.InvalidSignature, Claims: null);
        }

        var claims = ReadClaims(license, keyId);
        return claims is null
            ? new LicenseDocumentInspection(LicenseDocumentStatus.InvalidSignature, Claims: null)
            : new LicenseDocumentInspection(LicenseDocumentStatus.Valid, claims);
    }

    private static LicenseDocumentInspection Malformed() => new(LicenseDocumentStatus.Malformed, Claims: null);

    private static ErrorOr<SignedLicenseDocument> SigningFailed() =>
        Error.Failure("dependency_unavailable", "The license document could not be signed.");

    private static bool IsUsablePublicKey(string publicKey)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryLoad(string document, out License? license)
    {
        license = null;
        try
        {
            license = License.Load(document);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool HasValidSignature(License license, string publicKey)
    {
        try
        {
            return license.VerifySignature(publicKey);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool TryReadKeyId(string document, out string? keyId)
    {
        keyId = null;
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumDocumentLength,
            MaxCharactersFromEntities = 0,
            CloseInput = true,
        };

        try
        {
            using var reader = XmlReader.Create(new StringReader(document), settings);
            var root = XDocument.Load(reader, LoadOptions.None).Root;
            if (root is null || !string.Equals(root.Name.LocalName, "License", StringComparison.Ordinal))
            {
                return false;
            }

            var attributes = root.Elements().FirstOrDefault(element => string.Equals(element.Name.LocalName, "LicenseAttributes", StringComparison.Ordinal));
            keyId = attributes?.Elements()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, "Attribute", StringComparison.Ordinal)
                    && string.Equals((string?)element.Attribute("name"), KeyIdClaim, StringComparison.Ordinal))
                ?.Value;
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static LicenseDocumentClaims? ReadClaims(License license, string keyId)
    {
        var attributes = license.AdditionalAttributes;
        if (attributes is null)
        {
            return null;
        }

        var tenant = attributes.Get(TenantClaim);
        var order = attributes.Get(OrderClaim);
        var subscription = attributes.Get(SubscriptionClaim);
        var tier = attributes.Get(TierClaim);
        var addOns = attributes.Get(AddOnsClaim);
        var revision = attributes.Get(RevisionClaim);
        var signedKeyId = attributes.Get(KeyIdClaim);
        var expiry = attributes.Get(ExpiryClaim);
        if (!Guid.TryParseExact(tenant, "D", out var tenantId)
            || !Guid.TryParseExact(order, "D", out var orderId)
            || !Guid.TryParseExact(subscription, "D", out var subscriptionId)
            || !TryParseTier(tier, out var licenseTier)
            || !uint.TryParse(revision, NumberStyles.None, CultureInfo.InvariantCulture, out var licenseRevision)
            || licenseRevision == 0
            || !string.Equals(signedKeyId, keyId, StringComparison.Ordinal)
            || !DateTimeOffset.TryParseExact(expiry, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAt)
            || expiresAt.Offset != TimeSpan.Zero
            || addOns is null)
        {
            return null;
        }

        var codes = SplitAddOnCodes(addOns);
        var uniqueCodes = new HashSet<string>(codes, StringComparer.Ordinal);
        return codes.Any(code => code.Length is 0 or > 64) || uniqueCodes.Count != codes.Length
            ? null
            : new LicenseDocumentClaims(
                tenantId.ToString("D"),
                orderId,
                subscriptionId,
                licenseTier,
                codes,
                licenseRevision,
                keyId,
                expiresAt);
    }

    private static string[] SplitAddOnCodes(string addOns) =>
        addOns.Length == 0 ? [] : addOns.Split(',', StringSplitOptions.None);

    private static bool TryParseTier(string? tier, out LicenseTier licenseTier)
    {
        switch (tier)
        {
            case "shared":
                licenseTier = LicenseTier.Shared;
                return true;
            case "dedicated":
                licenseTier = LicenseTier.Dedicated;
                return true;
            default:
                licenseTier = default;
                return false;
        }
    }

    private static string ToWireTier(LicenseTier tier) => tier switch
    {
        LicenseTier.Shared => "shared",
        LicenseTier.Dedicated => "dedicated",
        _ => throw new ArgumentOutOfRangeException(nameof(tier)),
    };

    private bool CanSignWithActiveKey()
    {
        try
        {
            var probe = License.New()
                .WithUniqueIdentifier(Guid.NewGuid())
                .As(LicenseType.Standard)
                .ExpiresAt(DateTime.UtcNow.AddDays(1))
                .WithAdditionalAttributes(attributes => attributes.Add(KeyIdClaim, ActiveKeyId))
                .CreateAndSignWithPrivateKey(_activePrivateKey, _activePassPhrase);
            return probe.VerifySignature(_publicKeys[ActiveKeyId]);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
