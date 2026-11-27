using SharedKernel.Core.Domain;
using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Domain.Aggregates.PlacementProbe;

/// <summary>The single current probe-work row for one tenant and order.</summary>
public sealed class PlacementProbeWork : BaseEntity<Guid>, IAggregateRoot, ITenantScoped
{
    private PlacementProbeWork()
    {
    }

    /// <inheritdoc/>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Gets the owning order id.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Gets the evidence revision this work verifies.</summary>
    public uint EvidenceRevision { get; private set; }

    /// <summary>Gets the allowlisted placement id.</summary>
    public string PlacementId { get; private set; } = string.Empty;

    /// <summary>Gets when the work is next due, or null for terminal or parked work.</summary>
    public DateTimeOffset? DueAt { get; private set; }

    /// <summary>Gets the current lease token.</summary>
    public Guid? LeaseToken { get; private set; }

    /// <summary>Gets the current lease expiry.</summary>
    public DateTimeOffset? LeaseUntil { get; private set; }

    /// <summary>Gets a value indicating whether the current evidence failed terminally and must not be retried automatically.</summary>
    public bool Terminal { get; private set; }

    /// <summary>Gets the last sanitized failure classification.</summary>
    public FulfillmentFailureCode? LastFailureCode { get; private set; }

    /// <summary>Creates due work for the first evidence revision.</summary>
    /// <param name="tenantId">The validated tenant id.</param>
    /// <param name="orderId">The owning order id.</param>
    /// <param name="evidenceRevision">The evidence revision.</param>
    /// <param name="placementId">The allowlisted placement id.</param>
    /// <param name="dueAt">The first due instant.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>The new work row.</returns>
    public static PlacementProbeWork Create(
        string tenantId,
        Guid orderId,
        uint evidenceRevision,
        string placementId,
        DateTimeOffset dueAt,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(placementId);
        var work = new PlacementProbeWork
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            EvidenceRevision = evidenceRevision,
            PlacementId = placementId,
            DueAt = dueAt,
        };
        work.SetUpdatedProperties(now, updatedBy: null);
        return work;
    }

    /// <summary>Resets the row for new evidence without a lease and due immediately.</summary>
    /// <param name="evidenceRevision">The new evidence revision.</param>
    /// <param name="placementId">The new placement id.</param>
    /// <param name="dueAt">The immediate due instant.</param>
    /// <param name="now">The current UTC instant.</param>
    public void ResetForEvidence(uint evidenceRevision, string placementId, DateTimeOffset dueAt, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(placementId);
        EvidenceRevision = evidenceRevision;
        PlacementId = placementId;
        DueAt = dueAt;
        LeaseToken = null;
        LeaseUntil = null;
        Terminal = false;
        LastFailureCode = null;
        SetUpdatedProperties(now, updatedBy: null);
    }

    /// <summary>Requeues current nonterminal work immediately without reusing a prior proof or lease.</summary>
    /// <param name="dueAt">The immediate due instant.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>True when the parked work was requeued.</returns>
    public bool Requeue(DateTimeOffset dueAt, DateTimeOffset now)
    {
        if (Terminal || DueAt is not null)
        {
            return false;
        }

        DueAt = dueAt;
        LeaseToken = null;
        LeaseUntil = null;
        SetUpdatedProperties(now, updatedBy: null);
        return true;
    }

    /// <summary>Records a successful attempt and schedules the next refresh.</summary>
    /// <param name="nextDueAt">The next refresh instant.</param>
    /// <param name="now">The current UTC instant.</param>
    public void CompleteSuccess(DateTimeOffset nextDueAt, DateTimeOffset now)
    {
        DueAt = nextDueAt;
        LeaseToken = null;
        LeaseUntil = null;
        LastFailureCode = null;
        SetUpdatedProperties(now, updatedBy: null);
    }

    /// <summary>Records a retryable transient failure and schedules the next attempt.</summary>
    /// <param name="nextDueAt">The next attempt instant.</param>
    /// <param name="now">The current UTC instant.</param>
    public void CompleteTransientFailure(DateTimeOffset nextDueAt, DateTimeOffset now)
    {
        DueAt = nextDueAt;
        LeaseToken = null;
        LeaseUntil = null;
        Terminal = false;
        LastFailureCode = FulfillmentFailureCode.ProbeUnavailable;
        SetUpdatedProperties(now, updatedBy: null);
    }

    /// <summary>Records a terminal failure that must not be retried automatically.</summary>
    /// <param name="failureCode">The terminal classification.</param>
    /// <param name="now">The current UTC instant.</param>
    public void CompleteTerminalFailure(FulfillmentFailureCode failureCode, DateTimeOffset now)
    {
        DueAt = null;
        LeaseToken = null;
        LeaseUntil = null;
        Terminal = true;
        LastFailureCode = failureCode;
        SetUpdatedProperties(now, updatedBy: null);
    }

    /// <summary>Gets whether a token owns the current unexpired lease.</summary>
    /// <param name="token">The lease token.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>Whether the token owns a live lease.</returns>
    public bool HasLiveLease(Guid token, DateTimeOffset now) =>
        LeaseToken == token && LeaseUntil is { } until && until > now;

    /// <summary>Clears the lease after an attempt whose result was discarded.</summary>
    /// <param name="now">The current UTC instant.</param>
    public void ReleaseLease(DateTimeOffset now)
    {
        LeaseToken = null;
        LeaseUntil = null;
        SetUpdatedProperties(now, updatedBy: null);
    }
}
