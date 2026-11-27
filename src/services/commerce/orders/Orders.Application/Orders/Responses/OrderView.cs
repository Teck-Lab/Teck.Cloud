using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;
using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>Projection facts shared by command and query responses.</summary>
/// <param name="Id">The order id.</param>
/// <param name="SubscriptionId">The subscription id.</param>
/// <param name="Tier">The tier.</param>
/// <param name="AddOnCodes">The add-on codes.</param>
/// <param name="TotalAmountMinor">The immutable snapshot total in minor units.</param>
/// <param name="Currency">The immutable snapshot currency.</param>
/// <param name="Revision">The public order revision.</param>
/// <param name="CreatedAt">The creation instant.</param>
/// <param name="UpdatedAt">The last update instant.</param>
/// <param name="PaymentStatus">The stored payment state.</param>
/// <param name="PaidThrough">The paid-period end.</param>
/// <param name="ApprovalStatus">The approval state.</param>
/// <param name="ApprovalActorId">The deciding staff subject.</param>
/// <param name="ApprovalReason">The decision reason.</param>
/// <param name="ApprovalOccurredAt">The decision instant.</param>
/// <param name="EvidenceRevision">The evidence revision.</param>
/// <param name="EvidencePlacementId">The evidence placement id.</param>
/// <param name="EvidenceReference">The evidence reference.</param>
/// <param name="FulfillmentStatus">The stored fulfillment state.</param>
/// <param name="ProofEvidenceRevision">The probed evidence revision.</param>
/// <param name="ProofPlacementId">The probed placement id.</param>
/// <param name="ProofObservedAt">The probe instant.</param>
/// <param name="ProofFailureCode">The probe failure code.</param>
/// <param name="DesiredRevision">The desired generation.</param>
/// <param name="DesiredEnabled">Whether the desired generation enables entitlement.</param>
/// <param name="DesiredNotAfter">The desired validity end.</param>
/// <param name="LicenseState">The license outcome state.</param>
/// <param name="LicenseRevision">The signed license revision.</param>
/// <param name="LicenseKeyId">The signed license key id.</param>
/// <param name="LicenseExpiresAt">The signed license expiry.</param>
/// <param name="AppliedDesiredRevision">The desired generation the outcome applies to.</param>
internal sealed record OrderView(
    Guid Id,
    Guid SubscriptionId,
    Tier Tier,
    IReadOnlyList<string> AddOnCodes,
    long TotalAmountMinor,
    string Currency,
    uint Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    PaymentStatus PaymentStatus,
    DateTimeOffset? PaidThrough,
    ApprovalStatus ApprovalStatus,
    string? ApprovalActorId,
    string? ApprovalReason,
    DateTimeOffset? ApprovalOccurredAt,
    uint EvidenceRevision,
    string? EvidencePlacementId,
    string? EvidenceReference,
    FulfillmentStatus FulfillmentStatus,
    uint ProofEvidenceRevision,
    string? ProofPlacementId,
    DateTimeOffset? ProofObservedAt,
    FulfillmentFailureCode? ProofFailureCode,
    uint DesiredRevision,
    bool DesiredEnabled,
    DateTimeOffset? DesiredNotAfter,
    LicenseState LicenseState,
    uint? LicenseRevision,
    string? LicenseKeyId,
    DateTimeOffset? LicenseExpiresAt,
    uint AppliedDesiredRevision)
{
    internal bool IsPaid(DateTimeOffset now) => PaymentStatus == PaymentStatus.Paid && PaidThrough is { } end && end > now;

    internal bool HasFreshProof(DateTimeOffset now) =>
        EvidenceRevision > 0
        && FulfillmentStatus == FulfillmentStatus.Ready
        && ProofEvidenceRevision == EvidenceRevision
        && string.Equals(ProofPlacementId, EvidencePlacementId, StringComparison.Ordinal)
        && ProofObservedAt is { } observed
        && observed <= now
        && now < observed.Add(Order.ProofValidity);

    internal bool IsTerminalFailure() =>
        EvidenceRevision > 0
        && FulfillmentStatus == FulfillmentStatus.Failed
        && ProofEvidenceRevision == EvidenceRevision;

    internal bool HasCurrentGrant(DateTimeOffset now) =>
        DesiredEnabled
        && LicenseState == LicenseState.Active
        && AppliedDesiredRevision == DesiredRevision
        && LicenseExpiresAt is { } expiry
        && expiry > now;

    internal OrderStatus Status(DateTimeOffset now) => OrderStatusPolicy.Resolve(
        ApprovalStatus,
        IsPaid(now),
        HasFreshProof(now),
        HasCurrentGrant(now),
        IsTerminalFailure());

    internal FulfillmentStatus ProjectedFulfillment(DateTimeOffset now) =>
        EvidenceRevision switch
        {
            0 => FulfillmentStatus.AwaitingEvidence,
            _ when IsTerminalFailure() => FulfillmentStatus.Failed,
            _ when HasFreshProof(now) => FulfillmentStatus.Ready,
            _ => FulfillmentStatus.Verifying,
        };

    internal DateTimeOffset? ProjectedObservedAt(DateTimeOffset now)
    {
        var projected = ProjectedFulfillment(now);
        return projected is FulfillmentStatus.Ready or FulfillmentStatus.Failed ? ProofObservedAt : null;
    }

    internal FulfillmentFailureCode? ProjectedFailureCode(DateTimeOffset now)
    {
        var projected = ProjectedFulfillment(now);
        var failureCodeApplies = projected == FulfillmentStatus.Failed
            || (projected == FulfillmentStatus.Verifying && ProofFailureCode == FulfillmentFailureCode.ProbeUnavailable);
        return failureCodeApplies ? ProofFailureCode : null;
    }
}
