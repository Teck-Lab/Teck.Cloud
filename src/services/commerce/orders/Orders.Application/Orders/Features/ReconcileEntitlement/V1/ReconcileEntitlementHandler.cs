using ErrorOr;
using SharedKernel.Core.Database;
using Teck.Cloud.Commerce.Orders.Application.Orders.Entitlements;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;
using Wolverine;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.ReconcileEntitlement.V1;

/// <summary>Expired-entitlement reconciliation handler.</summary>
public static class ReconcileEntitlementHandler
{
    /// <summary>Disables or shortens a stale desired generation before network work.</summary>
    /// <param name="command">The command.</param>
    /// <param name="orders">The tracked order repository.</param>
    /// <param name="unitOfWork">The save boundary.</param>
    /// <param name="bus">The message bus.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>True when a new desired generation was recorded.</returns>
    public static Task<ErrorOr<bool>> HandleAsync(
        ReconcileEntitlementCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return ReconcileAsync(command, orders, unitOfWork, bus, clock, cancellationToken);
    }

    private static async Task<ErrorOr<bool>> ReconcileAsync(
        ReconcileEntitlementCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var order = await orders.LoadForUpdateAsync(command.OrderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return false;
        }

        var now = clock.GetUtcNow();
        var previous = order.DesiredRevision;
        var changed = order.RecomputeDesired(now);
        await LicenseDesiredPublisher.PublishIfChangedAsync(order, previous, bus, now, cancellationToken).ConfigureAwait(false);
        if (changed)
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return changed;
    }
}
