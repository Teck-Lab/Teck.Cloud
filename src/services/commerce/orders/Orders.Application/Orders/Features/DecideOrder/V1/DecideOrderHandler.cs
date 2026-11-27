using ErrorOr;
using SharedKernel.Core.Database;
using Teck.Cloud.Commerce.Orders.Application.Orders.Entitlements;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.OrderAudit;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;
using Wolverine;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.DecideOrder.V1;

/// <summary>Staff decision handler.</summary>
public static class DecideOrderHandler
{
    /// <summary>Applies the audited decision in one transaction.</summary>
    /// <param name="command">The command.</param>
    /// <param name="orders">The tracked order repository.</param>
    /// <param name="audit">The tracked audit repository.</param>
    /// <param name="unitOfWork">The save boundary.</param>
    /// <param name="bus">The message bus.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The updated admin projection, or a stable error.</returns>
    public static Task<ErrorOr<AdminOrderDto>> HandleAsync(
        DecideOrderCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IGenericWriteRepository<OrderAuditEntry, Guid> audit,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleCoreAsync(command, orders, audit, unitOfWork, bus, clock, cancellationToken);
    }

    private static async Task<ErrorOr<AdminOrderDto>> HandleCoreAsync(
        DecideOrderCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IGenericWriteRepository<OrderAuditEntry, Guid> audit,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var order = await orders.LoadForUpdateAsync(command.OrderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return OrderErrors.NotFound;
        }

        var previous = order.DesiredRevision;
        var now = clock.GetUtcNow();
        var decided = order.Decide(command.Decision, command.Reason, command.ActorId, command.ExpectedRevision, now);
        if (decided.IsError)
        {
            return decided.FirstError;
        }

        var entry = OrderAuditEntry.Create(
            command.TenantId,
            order.Id,
            command.ActorId,
            decided.Value,
            command.Reason.Trim(),
            now);
        await audit.AddAsync(entry, cancellationToken).ConfigureAwait(false);
        _ = order.RecomputeDesired(now);
        await LicenseDesiredPublisher.PublishIfChangedAsync(order, previous, bus, now, cancellationToken).ConfigureAwait(false);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var entries = await audit.FindAsync(candidate => candidate.OrderId == order.Id, cancellationToken).ConfigureAwait(false);
        return OrderProjection.Admin(
            OrderProjection.From(order),
            [.. entries.OrderBy(static item => item.OccurredAt).Select(OrderProjection.Audit)],
            now);
    }
}
