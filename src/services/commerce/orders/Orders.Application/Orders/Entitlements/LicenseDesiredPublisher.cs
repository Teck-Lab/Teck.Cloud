using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;
using Teck.Cloud.Commerce.Orders.Domain.Common;
using Wolverine;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Entitlements;

/// <summary>Publishes desired entitlement snapshots atomically through the Orders outbox.</summary>
public static class LicenseDesiredPublisher
{
    /// <summary>Publishes only when the desired generation changed.</summary>
    /// <param name="order">The mutated order.</param>
    /// <param name="previousDesiredRevision">The generation observed before the mutation.</param>
    /// <param name="bus">The message bus.</param>
    /// <param name="now">The current UTC instant.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>A task that completes when the message is recorded.</returns>
    public static Task PublishIfChangedAsync(
        Order order,
        uint previousDesiredRevision,
        IMessageBus bus,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(bus);
        return PublishIfChangedCoreAsync(order, previousDesiredRevision, bus, now, cancellationToken);
    }

    private static async Task PublishIfChangedCoreAsync(
        Order order,
        uint previousDesiredRevision,
        IMessageBus bus,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (order.DesiredRevision == previousDesiredRevision)
        {
            return;
        }

        var message = new LicenseDesiredEvent(
            Guid.NewGuid(),
            now,
            Guid.Parse(order.TenantId),
            order.Id,
            order.SubscriptionId,
            order.DesiredRevision,
            order.DesiredEnabled,
            order.DesiredTier == Tier.Dedicated ? "dedicated" : "shared",
            [.. order.AddOnCodes],
            order.DesiredNotAfter);
        await bus.PublishAsync(message, new DeliveryOptions { TenantId = order.TenantId }).ConfigureAwait(false);
        _ = cancellationToken;
    }
}
