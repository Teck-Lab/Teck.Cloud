using ErrorOr;
using SharedKernel.Core.Domain;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Baskets;
using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;

/// <summary>The authoritative commerce order with immutable checkout facts and separately tracked gates.</summary>
public sealed class Order : BaseEntity<Guid>, IAggregateRoot, ITenantScoped
{
    /// <summary>The maximum validity of a readiness proof.</summary>
    public static readonly TimeSpan ProofValidity = TimeSpan.FromMinutes(15);

    private Order()
    {
    }

    /// <inheritdoc/>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Gets the authenticated customer subject that owns the order.</summary>
    public string SubjectId { get; private set; } = string.Empty;

    /// <summary>Gets the immutable subscription binding.</summary>
    public Guid SubscriptionId { get; private set; }

    /// <summary>Gets the immutable catalog offer id.</summary>
    public string OfferId { get; private set; } = string.Empty;

    /// <summary>Gets the immutable database tier.</summary>
    public Tier Tier { get; private set; }

    /// <summary>Gets the immutable canonical stored add-on codes.</summary>
    public string AddOnCodesCsv { get; private set; } = string.Empty;

    /// <summary>Gets the sorted, deduplicated add-on codes.</summary>
    public IReadOnlyList<string> AddOnCodes => BasketSelection.DecodeAddOnCodes(AddOnCodesCsv);

    /// <summary>Gets the immutable snapshot total in minor units.</summary>
    public long TotalAmountMinor { get; private set; }

    /// <summary>Gets the immutable snapshot currency.</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>Gets the optimistic order revision.</summary>
    public uint Revision { get; private set; }

    /// <summary>Gets the canonical order-id sort key used for deterministic paging.</summary>
    public string SortKey { get; private set; } = string.Empty;

    /// <summary>Gets the immutable first checkout-intent instant.</summary>
    public DateTimeOffset CheckoutRequestedAt { get; private set; }

    /// <summary>Gets the number of provider checkout creation attempts started.</summary>
    public int CheckoutCreateAttempts { get; private set; }

    /// <summary>Gets when the last provider checkout creation attempt started.</summary>
    public DateTimeOffset? LastCheckoutAttemptAt { get; private set; }

    /// <summary>Gets the immutable verified provider price snapshot, or null before verification.</summary>
    public string? VerifiedPriceSnapshot { get; private set; }

    /// <summary>Gets the immutable server catalog price request snapshot.</summary>
    public string CatalogPriceSnapshot { get; private set; } = string.Empty;

    /// <summary>Gets the stable provider checkout idempotency key derived from the order id.</summary>
    public string ProviderIdempotencyKey { get; private set; } = string.Empty;

    /// <summary>Gets the immutable bound provider checkout session id.</summary>
    public string? CheckoutProviderSessionId { get; private set; }

    /// <summary>Gets the last provider-hosted checkout URL bound to the session.</summary>
    public string? CheckoutUrl { get; private set; }

    /// <summary>Gets when the provider session binding was recorded.</summary>
    public DateTimeOffset? CheckoutSessionBoundAt { get; private set; }

    /// <summary>Gets the current checkout work lease token.</summary>
    public Guid? CheckoutLeaseToken { get; private set; }

    /// <summary>Gets when the current checkout work lease expires.</summary>
    public DateTimeOffset? CheckoutLeaseUntil { get; private set; }

    /// <summary>Gets the sanitized category of the last checkout creation failure.</summary>
    public string? CheckoutLastErrorCategory { get; private set; }

    /// <summary>Gets the provider account mode bound to the checkout session.</summary>
    public string? CheckoutProviderMode { get; private set; }

    /// <summary>Gets the provider account id bound to the checkout verification.</summary>
    public string? ProviderAccountId { get; private set; }

    /// <summary>Gets the verified payment state.</summary>
    public PaymentStatus PaymentStatus { get; private set; } = PaymentStatus.Pending;

    /// <summary>Gets the authoritative paid-period end.</summary>
    public DateTimeOffset? PaidThrough { get; private set; }

    /// <summary>Gets the verified provider subscription id.</summary>
    public string? ProviderSubscriptionId { get; private set; }

    /// <summary>Gets the authoritative provider invoice id.</summary>
    public string? ProviderInvoiceId { get; private set; }

    /// <summary>Gets the verified provider charge id.</summary>
    public string? ProviderChargeId { get; private set; }

    /// <summary>Gets the verified provider payment-intent id.</summary>
    public string? ProviderPaymentIntentId { get; private set; }

    /// <summary>Gets the provider account mode used for verification.</summary>
    public string? ProviderMode { get; private set; }

    /// <summary>Gets the verified recurring price identifier multiset.</summary>
    public string? VerifiedPriceKey { get; private set; }

    /// <summary>Gets when the payment facts were last authoritatively verified.</summary>
    public DateTimeOffset? PaymentVerifiedAt { get; private set; }

    /// <summary>Gets the final staff approval state.</summary>
    public ApprovalStatus ApprovalStatus { get; private set; } = ApprovalStatus.Pending;

    /// <summary>Gets the authenticated staff actor that decided.</summary>
    public string? ApprovalActorId { get; private set; }

    /// <summary>Gets the trimmed decision reason.</summary>
    public string? ApprovalReason { get; private set; }

    /// <summary>Gets the server UTC decision instant.</summary>
    public DateTimeOffset? ApprovalOccurredAt { get; private set; }

    /// <summary>Gets the current placement evidence revision; zero means no evidence.</summary>
    public uint EvidenceRevision { get; private set; }

    /// <summary>Gets the current evidence placement id.</summary>
    public string? EvidencePlacementId { get; private set; }

    /// <summary>Gets the current nonsecret evidence reference.</summary>
    public string? EvidenceReference { get; private set; }

    /// <summary>Gets when the current evidence was recorded.</summary>
    public DateTimeOffset? EvidenceRecordedAt { get; private set; }

    /// <summary>Gets the stored probe result state.</summary>
    public FulfillmentStatus FulfillmentStatus { get; private set; } = FulfillmentStatus.AwaitingEvidence;

    /// <summary>Gets the evidence revision the stored probe result applies to.</summary>
    public uint ProofEvidenceRevision { get; private set; }

    /// <summary>Gets the placement id the stored probe result applies to.</summary>
    public string? ProofPlacementId { get; private set; }

    /// <summary>Gets the last successful probe instant, or the terminal attempt completion instant.</summary>
    public DateTimeOffset? ProofObservedAt { get; private set; }

    /// <summary>Gets the stored probe failure classification.</summary>
    public FulfillmentFailureCode? ProofFailureCode { get; private set; }

    /// <summary>Gets the monotonically increasing desired entitlement generation.</summary>
    public uint DesiredRevision { get; private set; }

    /// <summary>Gets a value indicating whether the current desired generation enables entitlement.</summary>
    public bool DesiredEnabled { get; private set; }

    /// <summary>Gets the desired tier.</summary>
    public Tier DesiredTier { get; private set; }

    /// <summary>Gets the desired stored add-on codes.</summary>
    public string DesiredAddOnCodesCsv { get; private set; } = string.Empty;

    /// <summary>Gets the current desired license validity end, or null when disabled.</summary>
    public DateTimeOffset? DesiredNotAfter { get; private set; }

    /// <summary>Gets when the desired snapshot last changed.</summary>
    public DateTimeOffset? DesiredUpdatedAt { get; private set; }

    /// <summary>Gets the last license outcome state.</summary>
    public LicenseState LicenseState { get; private set; } = LicenseState.Pending;

    /// <summary>Gets the signed license revision from the last accepted outcome.</summary>
    public uint? LicenseRevision { get; private set; }

    /// <summary>Gets the signed license key id from the last accepted outcome.</summary>
    public string? LicenseKeyId { get; private set; }

    /// <summary>Gets the signed license expiry from the last accepted outcome.</summary>
    public DateTimeOffset? LicenseExpiresAt { get; private set; }

    /// <summary>Gets the desired generation the last accepted license outcome applies to.</summary>
    public uint AppliedDesiredRevision { get; private set; }

    /// <summary>Creates an order with its immutable selection and price snapshot.</summary>
    /// <param name="tenantId">The validated tenant id.</param>
    /// <param name="subjectId">The authenticated subject.</param>
    /// <param name="orderId">The deterministic order id for the intent.</param>
    /// <param name="subscriptionId">The subscription id.</param>
    /// <param name="selection">The canonical selection.</param>
    /// <param name="tier">The effective tier.</param>
    /// <param name="total">The server-priced snapshot total.</param>
    /// <param name="catalogPriceSnapshot">The immutable serialized catalog price request.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>The new order.</returns>
    public static Order Create(
        string tenantId,
        string subjectId,
        Guid orderId,
        Guid subscriptionId,
        BasketSelection selection,
        Tier tier,
        Money total,
        string catalogPriceSnapshot,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogPriceSnapshot);
        ArgumentNullException.ThrowIfNull(selection);
        var order = new Order
        {
            Id = orderId,
            TenantId = tenantId,
            SubjectId = subjectId,
            SubscriptionId = subscriptionId,
            OfferId = selection.OfferId,
            Tier = tier,
            AddOnCodesCsv = selection.EncodeAddOnCodes(),
            TotalAmountMinor = total.AmountMinor,
            Currency = total.Currency,
            Revision = 1,
            SortKey = orderId.ToString("D"),
            CheckoutRequestedAt = now,
            CatalogPriceSnapshot = catalogPriceSnapshot,
            ProviderIdempotencyKey = "commerce-checkout-" + orderId.ToString("D"),
            DesiredTier = tier,
            DesiredAddOnCodesCsv = selection.EncodeAddOnCodes(),
        };
        order.SetUpdatedProperties(now, updatedBy: null);
        return order;
    }

    /// <summary>Records the first and final staff decision.</summary>
    /// <param name="decision">The decision.</param>
    /// <param name="reason">The trimmed nonblank reason.</param>
    /// <param name="actorId">The authenticated staff subject.</param>
    /// <param name="expectedRevision">The caller's expected order revision.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>The recorded audit action, or a stable error.</returns>
    public ErrorOr<AuditAction> Decide(Decision decision, string? reason, string actorId, uint expectedRevision, DateTimeOffset now)
    {
        if (expectedRevision != Revision)
        {
            return OrderErrors.StaleRevision;
        }

        if (ApprovalStatus != ApprovalStatus.Pending)
        {
            return OrderErrors.DecisionFinal;
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 512)
        {
            return OrderErrors.InvalidDetail;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ApprovalStatus = decision == Decision.Approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        ApprovalActorId = actorId;
        ApprovalReason = reason.Trim();
        ApprovalOccurredAt = now;
        Revision = checked(Revision + 1);
        SetUpdatedProperties(now, actorId);
        return decision == Decision.Approve ? AuditAction.Approve : AuditAction.Reject;
    }

    /// <summary>Records or replaces placement evidence and invalidates the prior proof immediately.</summary>
    /// <param name="placementId">The allowlisted placement id.</param>
    /// <param name="evidenceReference">The bounded nonsecret audit reference.</param>
    /// <param name="expectedRevision">The caller's expected order revision.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>Success, or a stable error.</returns>
    public ErrorOr<Success> RecordEvidence(string placementId, string evidenceReference, uint expectedRevision, DateTimeOffset now)
    {
        if (expectedRevision != Revision)
        {
            return OrderErrors.StaleRevision;
        }

        if (ApprovalStatus != ApprovalStatus.Approved || !IsPaidAt(now))
        {
            return OrderErrors.EvidenceNotAllowed;
        }

        if (string.IsNullOrWhiteSpace(placementId)
            || placementId.Length > 64
            || string.IsNullOrWhiteSpace(evidenceReference)
            || evidenceReference.Trim().Length > 512)
        {
            return OrderErrors.InvalidDetail;
        }

        EvidenceRevision = checked(EvidenceRevision + 1);
        EvidencePlacementId = placementId;
        EvidenceReference = evidenceReference.Trim();
        EvidenceRecordedAt = now;
        FulfillmentStatus = FulfillmentStatus.Verifying;
        ProofEvidenceRevision = 0;
        ProofPlacementId = null;
        ProofObservedAt = null;
        ProofFailureCode = null;
        Revision = checked(Revision + 1);
        SetUpdatedProperties(now, updatedBy: null);
        return Result.Success;
    }

    /// <summary>Applies a successful probe only for the current evidence revision and placement.</summary>
    /// <param name="evidenceRevision">The claimed evidence revision.</param>
    /// <param name="placementId">The claimed placement id.</param>
    /// <param name="observedAt">The probe success instant.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>True when the result was applied; stale or superseded results are discarded.</returns>
    public bool RecordProbeSuccess(uint evidenceRevision, string placementId, DateTimeOffset observedAt, DateTimeOffset now)
    {
        if (!IsCurrentEvidence(evidenceRevision, placementId) || observedAt > now)
        {
            return false;
        }

        FulfillmentStatus = FulfillmentStatus.Ready;
        ProofEvidenceRevision = evidenceRevision;
        ProofPlacementId = placementId;
        ProofObservedAt = observedAt;
        ProofFailureCode = null;
        Revision = checked(Revision + 1);
        SetUpdatedProperties(now, updatedBy: null);
        return true;
    }

    /// <summary>Applies a conclusive probe failure only for the current evidence revision and placement.</summary>
    /// <param name="evidenceRevision">The claimed evidence revision.</param>
    /// <param name="placementId">The claimed placement id.</param>
    /// <param name="failureCode">The failure classification.</param>
    /// <param name="observedAt">The attempt completion instant for terminal failures.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>True when the result was applied; stale or superseded results are discarded.</returns>
    public bool RecordProbeFailure(
        uint evidenceRevision,
        string placementId,
        FulfillmentFailureCode failureCode,
        DateTimeOffset observedAt,
        DateTimeOffset now)
    {
        if (!IsCurrentEvidence(evidenceRevision, placementId))
        {
            return false;
        }

        FulfillmentStatus = failureCode == FulfillmentFailureCode.ProbeUnavailable
            ? FulfillmentStatus.Verifying
            : FulfillmentStatus.Failed;
        ProofEvidenceRevision = evidenceRevision;
        ProofPlacementId = placementId;
        ProofObservedAt = failureCode == FulfillmentFailureCode.ProbeUnavailable ? null : observedAt;
        ProofFailureCode = failureCode;
        Revision = checked(Revision + 1);
        SetUpdatedProperties(now, updatedBy: null);
        return true;
    }

    /// <summary>Applies an authoritative provider payment snapshot without deleting prior facts.</summary>
    /// <param name="paidThrough">The authoritative current paid-period end, or null when unpaid.</param>
    /// <param name="providerSessionId">The authoritative provider session id.</param>
    /// <param name="providerSubscriptionId">The verified provider subscription id.</param>
    /// <param name="invoiceId">The authoritative invoice id.</param>
    /// <param name="chargeId">The verified provider charge id.</param>
    /// <param name="paymentIntentId">The verified provider payment-intent id.</param>
    /// <param name="mode">The verified account mode.</param>
    /// <param name="accountId">The verified provider account id.</param>
    /// <param name="priceKey">The verified recurring price multiset key.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>True when the visible payment facts changed.</returns>
    public bool ApplyPayment(
        DateTimeOffset? paidThrough,
        string? providerSessionId,
        string? providerSubscriptionId,
        string? invoiceId,
        string? chargeId,
        string? paymentIntentId,
        string? mode,
        string? accountId,
        string? priceKey,
        DateTimeOffset now)
    {
        var isPaid = paidThrough is { } end && end > now;
        var changed = isPaid != (PaymentStatus == PaymentStatus.Paid)
            || (isPaid && PaidThrough != paidThrough)
            || (providerSubscriptionId is not null && !string.Equals(ProviderSubscriptionId, providerSubscriptionId, StringComparison.Ordinal));
        PaymentStatus = isPaid ? PaymentStatus.Paid : PaymentStatus.Pending;
        PaidThrough = isPaid ? paidThrough : null;
        ProviderSubscriptionId = providerSubscriptionId ?? ProviderSubscriptionId;
        ProviderInvoiceId = invoiceId ?? ProviderInvoiceId;
        ProviderChargeId = chargeId ?? ProviderChargeId;
        ProviderPaymentIntentId = paymentIntentId ?? ProviderPaymentIntentId;
        ProviderMode = mode ?? ProviderMode;
        ProviderAccountId = accountId ?? ProviderAccountId;
        VerifiedPriceKey = priceKey ?? VerifiedPriceKey;
        PaymentVerifiedAt = now;
        if (CheckoutProviderSessionId is null && providerSessionId is not null)
        {
            CheckoutProviderSessionId = providerSessionId;
        }

        if (changed)
        {
            Revision = checked(Revision + 1);
            SetUpdatedProperties(now, updatedBy: null);
        }

        return changed;
    }

    /// <summary>Applies a license outcome only when it matches the current desired generation and binding.</summary>
    /// <param name="desiredRevision">The outcome's desired generation.</param>
    /// <param name="subscriptionId">The outcome's subscription binding.</param>
    /// <param name="state">The outcome state.</param>
    /// <param name="licenseRevision">The signed license revision.</param>
    /// <param name="keyId">The signing key id.</param>
    /// <param name="expiresAt">The signed expiry.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>True when the outcome was applied; superseded outcomes are discarded.</returns>
    public bool ApplyLicenseOutcome(
        uint desiredRevision,
        Guid subscriptionId,
        LicenseState state,
        uint? licenseRevision,
        string? keyId,
        DateTimeOffset? expiresAt,
        DateTimeOffset now)
    {
        if (desiredRevision != DesiredRevision || subscriptionId != SubscriptionId)
        {
            return false;
        }

        LicenseState = state;
        LicenseRevision = licenseRevision;
        LicenseKeyId = keyId;
        LicenseExpiresAt = expiresAt;
        AppliedDesiredRevision = desiredRevision;
        Revision = checked(Revision + 1);
        SetUpdatedProperties(now, updatedBy: null);
        return true;
    }

    /// <summary>Recomputes and records the effective desired generation when its snapshot changes.</summary>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>True when a new desired generation was recorded.</returns>
    public bool RecomputeDesired(DateTimeOffset now)
    {
        var paid = IsPaidAt(now);
        var fresh = HasFreshProof(now);
        var enabled = paid && ApprovalStatus == ApprovalStatus.Approved && fresh;
        DateTimeOffset? notAfter = null;
        if (enabled && PaidThrough is { } paidEnd && ProofObservedAt is { } observed)
        {
            var proofEnd = observed.Add(ProofValidity);
            notAfter = paidEnd <= proofEnd ? paidEnd : proofEnd;
        }

        var changed = enabled != DesiredEnabled
            || notAfter != DesiredNotAfter
            || DesiredTier != Tier
            || !string.Equals(DesiredAddOnCodesCsv, AddOnCodesCsv, StringComparison.Ordinal);
        if (!changed)
        {
            return false;
        }

        DesiredEnabled = enabled;
        DesiredNotAfter = notAfter;
        DesiredTier = Tier;
        DesiredAddOnCodesCsv = AddOnCodesCsv;
        DesiredRevision = checked(DesiredRevision + 1);
        DesiredUpdatedAt = now;
        return true;
    }

    /// <summary>Starts a provider checkout creation attempt before any network call.</summary>
    /// <param name="now">The current UTC instant.</param>
    public void BeginCheckoutAttempt(DateTimeOffset now)
    {
        CheckoutCreateAttempts = checked(CheckoutCreateAttempts + 1);
        LastCheckoutAttemptAt = now;
        SetUpdatedProperties(now, updatedBy: null);
    }

    /// <summary>Stores the verified provider price snapshot exactly once.</summary>
    /// <param name="snapshot">The canonical serialized verified lines.</param>
    /// <returns>True when stored or already identical; false for a conflicting later snapshot.</returns>
    public bool TrySetVerifiedPriceSnapshot(string snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot);
        if (VerifiedPriceSnapshot is { } existing)
        {
            return string.Equals(existing, snapshot, StringComparison.Ordinal);
        }

        VerifiedPriceSnapshot = snapshot;
        return true;
    }

    /// <summary>Gets whether automatic checkout creation must stop after an ambiguous provider create.</summary>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>Whether the provider idempotency retention window has elapsed.</returns>
    public bool IsCheckoutAmbiguityExpiredAt(DateTimeOffset now) =>
        CheckoutCreateAttempts > 0 && now > CheckoutRequestedAt.AddHours(24);

    /// <summary>Starts or verifies a checkout work lease.</summary>
    /// <param name="token">The new lease token.</param>
    /// <param name="until">The lease expiry.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>True when the lease was acquired or is already held by the same token.</returns>
    public bool TryBeginCheckoutLease(Guid token, DateTimeOffset until, DateTimeOffset now)
    {
        if (CheckoutLeaseToken is { } current && current != token && CheckoutLeaseUntil is { } expiry && expiry > now)
        {
            return false;
        }

        CheckoutLeaseToken = token;
        CheckoutLeaseUntil = until;
        return true;
    }

    /// <summary>Binds the unique immutable provider checkout session under a live lease.</summary>
    /// <param name="token">The lease token.</param>
    /// <param name="sessionId">The provider session id.</param>
    /// <param name="checkoutUrl">The provider-hosted HTTPS checkout URL.</param>
    /// <param name="mode">The verified provider account mode.</param>
    /// <param name="accountId">The verified provider account id.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>Success, or a stable error.</returns>
    public ErrorOr<Success> ApplyCheckoutSession(
        Guid token,
        string sessionId,
        string checkoutUrl,
        string? mode,
        string? accountId,
        DateTimeOffset now)
    {
        if (!HasLiveCheckoutLease(token, now))
        {
            return OrderErrors.StaleRevision;
        }

        if (string.IsNullOrWhiteSpace(sessionId) || !checkoutUrl.StartsWith("https://", StringComparison.Ordinal))
        {
            return OrderErrors.InvalidDetail;
        }

        if (CheckoutProviderSessionId is { } bound)
        {
            if (string.Equals(bound, sessionId, StringComparison.Ordinal))
            {
                CheckoutLeaseToken = null;
                CheckoutLeaseUntil = null;
                return Result.Success;
            }

            return OrderErrors.StaleRevision;
        }

        CheckoutProviderSessionId = sessionId;
        CheckoutUrl = checkoutUrl;
        CheckoutSessionBoundAt = now;
        CheckoutProviderMode = mode;
        ProviderAccountId = accountId ?? ProviderAccountId;
        CheckoutLeaseToken = null;
        CheckoutLeaseUntil = null;
        CheckoutLastErrorCategory = null;
        Revision = checked(Revision + 1);
        SetUpdatedProperties(now, updatedBy: null);
        return Result.Success;
    }

    /// <summary>Releases a checkout lease, optionally recording a sanitized failure category.</summary>
    /// <param name="token">The lease token.</param>
    /// <param name="errorCategory">The sanitized failure category, or null on success.</param>
    /// <param name="now">The current UTC instant.</param>
    public void ReleaseCheckoutLease(Guid token, string? errorCategory, DateTimeOffset now)
    {
        if (CheckoutLeaseToken != token)
        {
            return;
        }

        CheckoutLeaseToken = null;
        CheckoutLeaseUntil = null;
        CheckoutLastErrorCategory = errorCategory;
        SetUpdatedProperties(now, updatedBy: null);
    }

    /// <summary>Gets whether the checkout lease is current.</summary>
    /// <param name="token">The lease token.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>Whether the token owns an unexpired lease.</returns>
    public bool HasLiveCheckoutLease(Guid token, DateTimeOffset now) =>
        CheckoutLeaseToken == token && CheckoutLeaseUntil is { } until && until > now;

    /// <summary>Gets whether a current verified paid period exists.</summary>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>Whether the order is currently paid.</returns>
    public bool IsPaidAt(DateTimeOffset now) => PaymentStatus == PaymentStatus.Paid && PaidThrough is { } end && end > now;

    /// <summary>Gets whether the current evidence has a fresh successful proof.</summary>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>Whether the proof is current and unexpired.</returns>
    public bool HasFreshProof(DateTimeOffset now) =>
        EvidenceRevision > 0
        && FulfillmentStatus == FulfillmentStatus.Ready
        && ProofEvidenceRevision == EvidenceRevision
        && string.Equals(ProofPlacementId, EvidencePlacementId, StringComparison.Ordinal)
        && ProofObservedAt is { } observed
        && observed <= now
        && now < observed.Add(ProofValidity);

    /// <summary>Gets whether the current evidence has a committed terminal failure.</summary>
    /// <returns>Whether fulfillment is terminally failed.</returns>
    public bool IsTerminalFailure() =>
        EvidenceRevision > 0
        && FulfillmentStatus == FulfillmentStatus.Failed
        && ProofEvidenceRevision == EvidenceRevision;

    /// <summary>Gets whether the current desired generation has a current active grant.</summary>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>Whether the grant is current and unexpired.</returns>
    public bool HasCurrentGrantAt(DateTimeOffset now) =>
        DesiredEnabled
        && LicenseState == LicenseState.Active
        && AppliedDesiredRevision == DesiredRevision
        && LicenseExpiresAt is { } expiry
        && expiry > now;

    /// <summary>Gets the effective order status at an instant.</summary>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>The effective status.</returns>
    public OrderStatus StatusAt(DateTimeOffset now) => OrderStatusPolicy.Resolve(
        ApprovalStatus,
        IsPaidAt(now),
        HasFreshProof(now),
        HasCurrentGrantAt(now),
        IsTerminalFailure());

    /// <summary>Gets the Admin fulfillment status at an instant.</summary>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>The projected fulfillment status.</returns>
    public FulfillmentStatus FulfillmentStatusAt(DateTimeOffset now) =>
        (EvidenceRevision, Terminal: IsTerminalFailure(), Fresh: HasFreshProof(now)) switch
        {
            (0, _, _) => FulfillmentStatus.AwaitingEvidence,
            (_, true, _) => FulfillmentStatus.Failed,
            (_, _, true) => FulfillmentStatus.Ready,
            _ => FulfillmentStatus.Verifying,
        };

    /// <summary>Gets the projected probe observed-at value.</summary>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>The fresh success instant or terminal completion instant, otherwise null.</returns>
    public DateTimeOffset? ObservedAtAt(DateTimeOffset now) =>
        FulfillmentStatusAt(now) switch
        {
            FulfillmentStatus.AwaitingEvidence => null,
            FulfillmentStatus.Verifying => null,
            FulfillmentStatus.Ready => ProofObservedAt,
            FulfillmentStatus.Failed => ProofObservedAt,
            _ => null,
        };

    /// <summary>Gets the projected failure code for the current evidence.</summary>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>The failure code, or null when no current failure applies.</returns>
    public FulfillmentFailureCode? FailureCodeAt(DateTimeOffset now) =>
        FulfillmentStatusAt(now) switch
        {
            FulfillmentStatus.AwaitingEvidence => null,
            FulfillmentStatus.Verifying when ProofFailureCode == FulfillmentFailureCode.ProbeUnavailable => FulfillmentFailureCode.ProbeUnavailable,
            FulfillmentStatus.Verifying => null,
            FulfillmentStatus.Ready => null,
            FulfillmentStatus.Failed => ProofFailureCode,
            _ => null,
        };

    /// <summary>Gets whether the order can currently accept a checkout session.</summary>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>Whether checkout creation should run.</returns>
    public bool CanCheckoutAt(DateTimeOffset now) =>
        !IsPaidAt(now)
        && ApprovalStatus != ApprovalStatus.Rejected
        && CheckoutProviderSessionId is null
        && !IsCheckoutAmbiguityExpiredAt(now);

    /// <summary>Gets whether a usable provider checkout URL is currently bound.</summary>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>Whether a payable bound URL can be returned.</returns>
    public bool HasUsableCheckoutUrlAt(DateTimeOffset now) =>
        !IsPaidAt(now)
        && ApprovalStatus != ApprovalStatus.Rejected
        && CheckoutProviderSessionId is not null
        && !string.IsNullOrWhiteSpace(CheckoutUrl);

    /// <summary>Gets the desired generation only when the order is active.</summary>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>The positive desired generation, otherwise null.</returns>
    public uint? DesiredRevisionWhenActiveAt(DateTimeOffset now) =>
        StatusAt(now) == OrderStatus.Active && DesiredRevision > 0 ? DesiredRevision : null;

    private bool IsCurrentEvidence(uint evidenceRevision, string placementId) =>
        EvidenceRevision > 0
        && evidenceRevision == EvidenceRevision
        && string.Equals(EvidencePlacementId, placementId, StringComparison.Ordinal);
}
