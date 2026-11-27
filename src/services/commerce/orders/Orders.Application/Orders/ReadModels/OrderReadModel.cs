using SharedKernel.Core.Domain;
using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.ReadModels;

/// <summary>Tenant-filtered order projection used by list and detail queries.</summary>
public sealed class OrderReadModel : ReadModelBase<Guid>, ITenantScoped
{
    /// <inheritdoc/>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Gets or sets the owning subject.</summary>
    public string SubjectId { get; set; } = string.Empty;

    /// <summary>Gets or sets the subscription id.</summary>
    public Guid SubscriptionId { get; set; }

    /// <summary>Gets or sets the catalog offer id.</summary>
    public string OfferId { get; set; } = string.Empty;

    /// <summary>Gets or sets the tier.</summary>
    public Tier Tier { get; set; }

    /// <summary>Gets or sets the canonical stored add-on codes.</summary>
    public string AddOnCodesCsv { get; set; } = string.Empty;

    /// <summary>Gets or sets the immutable total in minor units.</summary>
    public long TotalAmountMinor { get; set; }

    /// <summary>Gets or sets the immutable currency.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets the public order revision.</summary>
    public uint Revision { get; set; }

    /// <summary>Gets or sets the canonical order-id sort key.</summary>
    public string SortKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the payment state.</summary>
    public PaymentStatus PaymentStatus { get; set; }

    /// <summary>Gets or sets the paid-period end.</summary>
    public DateTimeOffset? PaidThrough { get; set; }

    /// <summary>Gets or sets the approval state.</summary>
    public ApprovalStatus ApprovalStatus { get; set; }

    /// <summary>Gets or sets the deciding staff subject.</summary>
    public string? ApprovalActorId { get; set; }

    /// <summary>Gets or sets the decision reason.</summary>
    public string? ApprovalReason { get; set; }

    /// <summary>Gets or sets the decision instant.</summary>
    public DateTimeOffset? ApprovalOccurredAt { get; set; }

    /// <summary>Gets or sets the evidence revision.</summary>
    public uint EvidenceRevision { get; set; }

    /// <summary>Gets or sets the evidence placement id.</summary>
    public string? EvidencePlacementId { get; set; }

    /// <summary>Gets or sets the evidence reference.</summary>
    public string? EvidenceReference { get; set; }

    /// <summary>Gets or sets the stored fulfillment state.</summary>
    public FulfillmentStatus FulfillmentStatus { get; set; }

    /// <summary>Gets or sets the probed evidence revision.</summary>
    public uint ProofEvidenceRevision { get; set; }

    /// <summary>Gets or sets the probed placement id.</summary>
    public string? ProofPlacementId { get; set; }

    /// <summary>Gets or sets the probe instant.</summary>
    public DateTimeOffset? ProofObservedAt { get; set; }

    /// <summary>Gets or sets the probe failure code.</summary>
    public FulfillmentFailureCode? ProofFailureCode { get; set; }

    /// <summary>Gets or sets the desired generation.</summary>
    public uint DesiredRevision { get; set; }

    /// <summary>Gets or sets a value indicating whether the desired generation enables entitlement.</summary>
    public bool DesiredEnabled { get; set; }

    /// <summary>Gets or sets the desired validity end.</summary>
    public DateTimeOffset? DesiredNotAfter { get; set; }

    /// <summary>Gets or sets the license outcome state.</summary>
    public LicenseState LicenseState { get; set; }

    /// <summary>Gets or sets the signed license revision.</summary>
    public uint? LicenseRevision { get; set; }

    /// <summary>Gets or sets the signed license key id.</summary>
    public string? LicenseKeyId { get; set; }

    /// <summary>Gets or sets the signed license expiry.</summary>
    public DateTimeOffset? LicenseExpiresAt { get; set; }

    /// <summary>Gets or sets the desired generation the outcome applies to.</summary>
    public uint AppliedDesiredRevision { get; set; }
}
