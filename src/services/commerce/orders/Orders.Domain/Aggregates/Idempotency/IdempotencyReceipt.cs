using SharedKernel.Core.Domain;

namespace Teck.Cloud.Commerce.Orders.Domain.Aggregates.Idempotency;

/// <summary>The durable checkout intent receipt, unique per tenant, subject and idempotency key.</summary>
public sealed class IdempotencyReceipt : BaseEntity<Guid>, IAggregateRoot, ITenantScoped
{
    private IdempotencyReceipt()
    {
    }

    /// <inheritdoc/>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Gets the authenticated subject.</summary>
    public string SubjectId { get; private set; } = string.Empty;

    /// <summary>Gets the canonical idempotency key.</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    /// <summary>Gets the original basket revision bound to the intent.</summary>
    public uint BasketRevision { get; private set; }

    /// <summary>Gets the created order id.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Creates the receipt in the same transaction as its order.</summary>
    /// <param name="tenantId">The validated tenant id.</param>
    /// <param name="subjectId">The authenticated subject.</param>
    /// <param name="idempotencyKey">The canonical key.</param>
    /// <param name="basketRevision">The original basket revision.</param>
    /// <param name="orderId">The created order id.</param>
    /// <returns>The receipt.</returns>
    public static IdempotencyReceipt Create(
        string tenantId,
        string subjectId,
        string idempotencyKey,
        uint basketRevision,
        Guid orderId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        return new IdempotencyReceipt
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubjectId = subjectId,
            IdempotencyKey = idempotencyKey,
            BasketRevision = basketRevision,
            OrderId = orderId,
        };
    }
}
