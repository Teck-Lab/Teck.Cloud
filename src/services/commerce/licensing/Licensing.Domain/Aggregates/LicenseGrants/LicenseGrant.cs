using ErrorOr;
using SharedKernel.Core.Domain;

namespace Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGrants;

/// <summary>One immutable entitlement grant for an accepted desired generation.</summary>
public sealed class LicenseGrant : BaseEntity, IAggregateRoot, ITenantScoped
{
    private const int MaximumDocumentLength = 65_536;
    private const int MaximumAddOns = 20;
    private const int MaximumKeyIdLength = 64;
    private const int MaximumAddOnCodeLength = 64;

    private LicenseGrant()
    {
    }

    /// <inheritdoc/>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Gets the order this grant is bound to.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Gets the subscription this grant is bound to.</summary>
    public Guid SubscriptionId { get; private set; }

    /// <summary>Gets the Orders desired entitlement generation this grant answers.</summary>
    public uint DesiredRevision { get; private set; }

    /// <summary>Gets the effective database tier recorded in the grant.</summary>
    public LicenseTier Tier { get; private set; }

    /// <summary>Gets the canonical, sorted and deduplicated add-on codes.</summary>
    public string CanonicalAddOnCodes { get; private set; } = string.Empty;

    /// <summary>Gets the grant lifecycle state.</summary>
    public LicenseGrantState State { get; private set; }

    /// <summary>Gets the monotonic signed-license revision for active grants.</summary>
    public uint? LicenseRevision { get; private set; }

    /// <summary>Gets the opaque signing key identifier for active grants.</summary>
    public string? KeyId { get; private set; }

    /// <summary>Gets the exact signed-document expiry for active grants.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>Gets the signed Standard.Licensing XML document for active grants.</summary>
    public string? Document { get; private set; }

    /// <summary>Gets the UTC instant at which this grant was issued.</summary>
    public DateTimeOffset IssuedAt { get; private set; }

    /// <summary>Gets the integration message that produced this grant.</summary>
    public Guid SourceMessageId { get; private set; }

    /// <summary>Gets the canonical sorted add-on codes.</summary>
    public IReadOnlyList<string> AddOnCodes => CanonicalAddOnCodes.Length == 0
        ? []
        : CanonicalAddOnCodes.Split(',', StringSplitOptions.None);

    /// <summary>Creates an active grant containing a freshly signed document.</summary>
    /// <param name="tenantId">The immutable organization UUID.</param>
    /// <param name="orderId">The bound order.</param>
    /// <param name="subscriptionId">The bound subscription.</param>
    /// <param name="desiredRevision">The accepted Orders generation.</param>
    /// <param name="tier">The effective database tier.</param>
    /// <param name="addOnCodes">The desired add-on codes.</param>
    /// <param name="licenseRevision">The monotonic signed-license revision.</param>
    /// <param name="keyId">The opaque signing key identifier.</param>
    /// <param name="expiresAt">The exact UTC expiry already clamped by Orders.</param>
    /// <param name="document">The signed Standard.Licensing document.</param>
    /// <param name="issuedAt">The verified server UTC issue instant.</param>
    /// <param name="sourceMessageId">The consumed desired-state message.</param>
    /// <returns>The active grant or a stable invariant error.</returns>
    public static ErrorOr<LicenseGrant> IssueActive(
        string tenantId,
        Guid orderId,
        Guid subscriptionId,
        uint desiredRevision,
        LicenseTier tier,
        IReadOnlyList<string> addOnCodes,
        uint licenseRevision,
        string keyId,
        DateTimeOffset expiresAt,
        string document,
        DateTimeOffset issuedAt,
        Guid sourceMessageId)
    {
        var binding = ValidateBinding(tenantId, orderId, subscriptionId, desiredRevision, tier, addOnCodes, sourceMessageId);
        if (binding.IsError)
        {
            return binding.Errors;
        }

        if (licenseRevision == 0)
        {
            return LicenseGrantErrors.InvalidRevision;
        }

        if (!IsValidKeyId(keyId))
        {
            return LicenseGrantErrors.InvalidDocument;
        }

        var normalizedExpiry = expiresAt.ToUniversalTime();
        if (normalizedExpiry <= issuedAt.ToUniversalTime())
        {
            return LicenseGrantErrors.InvalidExpiry;
        }

        var invalidDocument = string.IsNullOrWhiteSpace(document) || document.Length > MaximumDocumentLength;
        return invalidDocument
            ? LicenseGrantErrors.InvalidDocument
            : new LicenseGrant
            {
                Id = Guid.NewGuid(),
                TenantId = binding.Value.TenantId,
                OrderId = orderId,
                SubscriptionId = subscriptionId,
                DesiredRevision = desiredRevision,
                Tier = tier,
                CanonicalAddOnCodes = binding.Value.AddOnCodes,
                State = LicenseGrantState.Active,
                LicenseRevision = licenseRevision,
                KeyId = keyId,
                ExpiresAt = normalizedExpiry,
                Document = document,
                IssuedAt = issuedAt.ToUniversalTime(),
                SourceMessageId = sourceMessageId,
            };
    }

    /// <summary>Creates an inactive grant for a generation whose entitlement is disabled.</summary>
    /// <param name="tenantId">The immutable organization UUID.</param>
    /// <param name="orderId">The bound order.</param>
    /// <param name="subscriptionId">The bound subscription.</param>
    /// <param name="desiredRevision">The accepted Orders generation.</param>
    /// <param name="tier">The requested database tier from the snapshot.</param>
    /// <param name="addOnCodes">The requested add-on codes from the snapshot.</param>
    /// <param name="issuedAt">The verified server UTC instant.</param>
    /// <param name="sourceMessageId">The consumed desired-state message.</param>
    /// <returns>The inactive grant or a stable invariant error.</returns>
    public static ErrorOr<LicenseGrant> IssueInactive(
        string tenantId,
        Guid orderId,
        Guid subscriptionId,
        uint desiredRevision,
        LicenseTier tier,
        IReadOnlyList<string> addOnCodes,
        DateTimeOffset issuedAt,
        Guid sourceMessageId)
    {
        var binding = ValidateBinding(tenantId, orderId, subscriptionId, desiredRevision, tier, addOnCodes, sourceMessageId);
        return binding.IsError
            ? binding.Errors
            : new LicenseGrant
            {
                Id = Guid.NewGuid(),
                TenantId = binding.Value.TenantId,
                OrderId = orderId,
                SubscriptionId = subscriptionId,
                DesiredRevision = desiredRevision,
                Tier = tier,
                CanonicalAddOnCodes = binding.Value.AddOnCodes,
                State = LicenseGrantState.Inactive,
                IssuedAt = issuedAt.ToUniversalTime(),
                SourceMessageId = sourceMessageId,
            };
    }

    /// <summary>Marks an active grant as superseded by a higher desired generation.</summary>
    /// <returns>True when the grant changed state.</returns>
    public bool Supersede()
    {
        if (State != LicenseGrantState.Active)
        {
            return false;
        }

        State = LicenseGrantState.Superseded;
        return true;
    }

    /// <summary>Checks the grant is still usable for its exact generation and UTC instant.</summary>
    /// <param name="desiredRevision">The requested desired generation.</param>
    /// <param name="utcNow">The current UTC instant.</param>
    /// <returns>True when this grant is an active, current, unexpired document.</returns>
    public bool IsUsable(uint desiredRevision, DateTimeOffset utcNow) =>
        State == LicenseGrantState.Active
        && DesiredRevision == desiredRevision
        && LicenseRevision is > 0
        && KeyId is not null
        && Document is not null
        && ExpiresAt is { } expires
        && expires > utcNow.ToUniversalTime();

    private static ErrorOr<(string TenantId, string AddOnCodes)> ValidateBinding(
        string tenantId,
        Guid orderId,
        Guid subscriptionId,
        uint desiredRevision,
        LicenseTier tier,
        IReadOnlyList<string> addOnCodes,
        Guid sourceMessageId)
    {
        if (!Guid.TryParseExact(tenantId, "D", out var tenant) || orderId == Guid.Empty || subscriptionId == Guid.Empty || sourceMessageId == Guid.Empty)
        {
            return LicenseGrantErrors.InvalidBinding;
        }

        if (desiredRevision == 0)
        {
            return LicenseGrantErrors.InvalidRevision;
        }

        if (!Enum.IsDefined(tier))
        {
            return LicenseGrantErrors.InvalidTier;
        }

        if (addOnCodes is null || addOnCodes.Count > MaximumAddOns || addOnCodes.Any(code => !IsValidAddOnCode(code)))
        {
            return LicenseGrantErrors.InvalidAddOns;
        }

        var canonical = string.Join(',', addOnCodes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        return (tenant.ToString("D"), canonical);
    }

    private static bool IsValidAddOnCode(string? code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return false;
        }

        // The former pattern ended in '$', which also matches immediately before one
        // final line feed, so that single optional '\n' stays accepted after the code.
        var contentLength = code[^1] == '\n' ? code.Length - 1 : code.Length;
        if (contentLength == 0 || contentLength > MaximumAddOnCodeLength || !char.IsAsciiLetter(code[0]))
        {
            return false;
        }

        for (var index = 1; index < contentLength; index++)
        {
            var character = code[index];
            if (!char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidKeyId(string keyId) =>
        !string.IsNullOrWhiteSpace(keyId)
        && keyId.Length <= MaximumKeyIdLength
        && !keyId.Any(char.IsControl);
}
