using SharedKernel.Core.Domain;
using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Domain.Aggregates.OrderAudit;

/// <summary>An append-only staff audit record written in the same transaction as its decision.</summary>
public sealed class OrderAuditEntry : BaseEntity<Guid>, IAggregateRoot, ITenantScoped
{
    private OrderAuditEntry()
    {
    }

    /// <inheritdoc/>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Gets the audited order id.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Gets the authenticated staff subject.</summary>
    public string ActorId { get; private set; } = string.Empty;

    /// <summary>Gets the recorded action.</summary>
    public AuditAction Action { get; private set; }

    /// <summary>Gets the sanitized reason or evidence reference.</summary>
    public string? Reason { get; private set; }

    /// <summary>Gets the server UTC instant.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Creates an audit record.</summary>
    /// <param name="tenantId">The validated tenant id.</param>
    /// <param name="orderId">The audited order id.</param>
    /// <param name="actorId">The authenticated staff subject.</param>
    /// <param name="action">The recorded action.</param>
    /// <param name="reason">The sanitized reason or evidence reference.</param>
    /// <param name="occurredAt">The server UTC instant.</param>
    /// <returns>The audit record.</returns>
    public static OrderAuditEntry Create(
        string tenantId,
        Guid orderId,
        string actorId,
        AuditAction action,
        string? reason,
        DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        return new OrderAuditEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            ActorId = actorId,
            Action = action,
            Reason = reason,
            OccurredAt = occurredAt,
        };
    }
}
