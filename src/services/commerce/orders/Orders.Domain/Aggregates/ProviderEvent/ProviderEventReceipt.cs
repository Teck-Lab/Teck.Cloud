using SharedKernel.Core.Domain;
using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Domain.Aggregates.ProviderEvent;

/// <summary>A durable receipt for one verified provider event, unique by account and event id.</summary>
public sealed class ProviderEventReceipt : BaseEntity<Guid>, IAggregateRoot
{
    private ProviderEventReceipt()
    {
    }

    /// <summary>Gets the verified provider account id.</summary>
    public string AccountId { get; private set; } = string.Empty;

    /// <summary>Gets the unique provider event id.</summary>
    public string EventId { get; private set; } = string.Empty;

    /// <summary>Gets the provider event type.</summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>Gets the SHA-256 hash of the verified raw payload.</summary>
    public string PayloadHash { get; private set; } = string.Empty;

    /// <summary>Gets when the receipt was accepted.</summary>
    public DateTimeOffset ReceivedAt { get; private set; }

    /// <summary>Gets the durable processing status.</summary>
    public ProviderReceiptStatus Status { get; private set; } = ProviderReceiptStatus.Received;

    /// <summary>Gets the number of reconciliation attempts.</summary>
    public int Attempts { get; private set; }

    /// <summary>Gets when processing completed, failed or was found unmatched.</summary>
    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>Gets the resolved order id, when the event bound to one.</summary>
    public Guid? OrderId { get; private set; }

    /// <summary>Gets the resolved tenant id, when the event bound to one.</summary>
    public string? TenantId { get; private set; }

    /// <summary>Gets the sanitized failure category; never provider text or secrets.</summary>
    public string? LastErrorCategory { get; private set; }

    /// <summary>Creates a received receipt.</summary>
    /// <param name="accountId">The verified provider account id.</param>
    /// <param name="eventId">The unique provider event id.</param>
    /// <param name="eventType">The provider event type.</param>
    /// <param name="payloadHash">The SHA-256 payload hash.</param>
    /// <param name="orderId">The resolved order id, when already known.</param>
    /// <param name="tenantId">The resolved tenant id, when already known.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <returns>The new receipt.</returns>
    public static ProviderEventReceipt Create(
        string accountId,
        string eventId,
        string eventType,
        string payloadHash,
        Guid? orderId,
        string? tenantId,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        return new ProviderEventReceipt
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            EventId = eventId,
            EventType = eventType,
            PayloadHash = payloadHash,
            ReceivedAt = now,
            OrderId = orderId,
            TenantId = tenantId,
        };
    }

    /// <summary>Binds the receipt to a resolved order and tenant.</summary>
    /// <param name="orderId">The resolved order id.</param>
    /// <param name="tenantId">The resolved tenant id.</param>
    public void Bind(Guid orderId, string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        OrderId = orderId;
        TenantId = tenantId;
    }

    /// <summary>Marks the receipt as being reconciled and increments the attempt count.</summary>
    public void MarkProcessing() => Status = ProviderReceiptStatus.Processing;

    /// <summary>Marks the receipt completed.</summary>
    /// <param name="now">The current UTC instant.</param>
    public void MarkCompleted(DateTimeOffset now)
    {
        Status = ProviderReceiptStatus.Completed;
        ProcessedAt = now;
        LastErrorCategory = null;
        Attempts = checked(Attempts + 1);
    }

    /// <summary>Marks the receipt unmatched because no bound order could be verified.</summary>
    /// <param name="now">The current UTC instant.</param>
    public void MarkUnmatched(DateTimeOffset now)
    {
        Status = ProviderReceiptStatus.Unmatched;
        ProcessedAt = now;
        Attempts = checked(Attempts + 1);
    }
}
