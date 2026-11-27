using SharedKernel.Core.Domain;
using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Domain.Aggregates.Reconciliation;

/// <summary>Durable leased provider work for one order and work kind. Rows are global ops data with an explicit tenant.</summary>
public sealed class ReconciliationJob : BaseEntity<Guid>, IAggregateRoot
{
    private ReconciliationJob()
    {
    }

    /// <summary>Gets the owning tenant id; every handler verifies it explicitly.</summary>
    public string TenantId { get; private set; } = string.Empty;

    /// <summary>Gets the owning order id.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Gets the provider work kind.</summary>
    public ReconciliationKind Kind { get; private set; }

    /// <summary>Gets the triggering provider event receipt id, when applicable.</summary>
    public Guid? ProviderEventReceiptId { get; private set; }

    /// <summary>Gets the durable work status.</summary>
    public ReconciliationStatus Status { get; private set; } = ReconciliationStatus.Pending;

    /// <summary>Gets when the work is next due.</summary>
    public DateTimeOffset DueAt { get; private set; }

    /// <summary>Gets the number of attempts started.</summary>
    public int Attempts { get; private set; }

    /// <summary>Gets the current lease token.</summary>
    public Guid? LeaseToken { get; private set; }

    /// <summary>Gets the current lease expiry.</summary>
    public DateTimeOffset? LeaseUntil { get; private set; }

    /// <summary>Gets when the work completed or was abandoned.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Gets the sanitized failure category; never provider text or secrets.</summary>
    public string? LastErrorCategory { get; private set; }

    /// <summary>Creates due work.</summary>
    /// <param name="tenantId">The validated tenant id.</param>
    /// <param name="orderId">The owning order id.</param>
    /// <param name="kind">The work kind.</param>
    /// <param name="dueAt">The first due instant.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <param name="providerEventReceiptId">The triggering provider event receipt id, when applicable.</param>
    /// <returns>The new work row.</returns>
    public static ReconciliationJob Create(
        string tenantId,
        Guid orderId,
        ReconciliationKind kind,
        DateTimeOffset dueAt,
        DateTimeOffset now,
        Guid? providerEventReceiptId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        var job = new ReconciliationJob
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            Kind = kind,
            DueAt = dueAt,
            ProviderEventReceiptId = providerEventReceiptId,
        };
        job.SetUpdatedProperties(now, updatedBy: null);
        return job;
    }

    /// <summary>Reschedules pending work, clearing any lease.</summary>
    /// <param name="dueAt">The next due instant.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>True when the work was rescheduled.</returns>
    public bool Reschedule(DateTimeOffset dueAt, DateTimeOffset now)
    {
        if (Status != ReconciliationStatus.Pending)
        {
            return false;
        }

        DueAt = dueAt;
        LeaseToken = null;
        LeaseUntil = null;
        SetUpdatedProperties(now, updatedBy: null);
        return true;
    }

    /// <summary>Records a failed attempt with a sanitized category and schedules the retry.</summary>
    /// <param name="dueAt">The next attempt instant.</param>
    /// <param name="category">The sanitized failure category.</param>
    /// <param name="now">The current UTC instant.</param>
    public void RecordFailure(DateTimeOffset dueAt, string category, DateTimeOffset now)
    {
        Attempts = checked(Attempts + 1);
        DueAt = dueAt;
        LeaseToken = null;
        LeaseUntil = null;
        LastErrorCategory = category;
        SetUpdatedProperties(now, updatedBy: null);
    }

    /// <summary>Marks the work completed.</summary>
    /// <param name="now">The current UTC instant.</param>
    public void Complete(DateTimeOffset now)
    {
        Attempts = checked(Attempts + 1);
        Status = ReconciliationStatus.Completed;
        CompletedAt = now;
        LeaseToken = null;
        LeaseUntil = null;
        LastErrorCategory = null;
        SetUpdatedProperties(now, updatedBy: null);
    }

    /// <summary>Marks the work abandoned with a sanitized category.</summary>
    /// <param name="category">The sanitized category.</param>
    /// <param name="now">The current UTC instant.</param>
    public void Abandon(string category, DateTimeOffset now)
    {
        Attempts = checked(Attempts + 1);
        Status = ReconciliationStatus.Abandoned;
        CompletedAt = now;
        LeaseToken = null;
        LeaseUntil = null;
        LastErrorCategory = category;
        SetUpdatedProperties(now, updatedBy: null);
    }

    /// <summary>Gets whether a token owns the current unexpired lease.</summary>
    /// <param name="token">The lease token.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>Whether the token owns a live lease.</returns>
    public bool HasLiveLease(Guid token, DateTimeOffset now) =>
        LeaseToken == token && LeaseUntil is { } until && until > now;

    /// <summary>Gets whether the job can be claimed at an instant.</summary>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>Whether the job is pending, due and unleased.</returns>
    public bool IsClaimableAt(DateTimeOffset now) =>
        Status == ReconciliationStatus.Pending
        && DueAt <= now
        && (LeaseUntil is null || LeaseUntil <= now);
}
