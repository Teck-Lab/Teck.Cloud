using ErrorOr;
using SharedKernel.Core.Database;
using Teck.Cloud.Commerce.Orders.Application.Orders.Entitlements;
using Teck.Cloud.Commerce.Orders.Application.Orders.Placements;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.OrderAudit;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.PlacementProbe;
using Teck.Cloud.Commerce.Orders.Domain.Common;
using Wolverine;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.RecordPlacementEvidence.V1;

/// <summary>Placement evidence handler.</summary>
public static class RecordPlacementEvidenceHandler
{
    /// <summary>Applies evidence, resets work and disables the superseded generation in one transaction.</summary>
    /// <param name="command">The command.</param>
    /// <param name="orders">The tracked order repository.</param>
    /// <param name="audit">The tracked audit repository.</param>
    /// <param name="work">The tracked probe-work repository.</param>
    /// <param name="placement">The configured placement allowlist.</param>
    /// <param name="unitOfWork">The save boundary.</param>
    /// <param name="bus">The message bus.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The updated admin projection, or a stable error.</returns>
    public static Task<ErrorOr<AdminOrderDto>> HandleAsync(
        RecordPlacementEvidenceCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IGenericWriteRepository<OrderAuditEntry, Guid> audit,
        IGenericWriteRepository<PlacementProbeWork, Guid> work,
        IPlacementProbe placement,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleCoreAsync(command, orders, audit, work, placement, unitOfWork, bus, clock, cancellationToken);
    }

    private static async Task<ErrorOr<AdminOrderDto>> HandleCoreAsync(
        RecordPlacementEvidenceCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IGenericWriteRepository<OrderAuditEntry, Guid> audit,
        IGenericWriteRepository<PlacementProbeWork, Guid> work,
        IPlacementProbe placement,
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

        var registration = PlacementPolicy.Resolve(order, placement, command.PlacementId);
        if (registration.IsError)
        {
            return registration.FirstError;
        }

        var now = clock.GetUtcNow();
        var previous = order.DesiredRevision;
        var recorded = order.RecordEvidence(command.PlacementId, command.EvidenceReference, command.ExpectedRevision, now);
        if (recorded.IsError)
        {
            return recorded.FirstError;
        }

        await audit.AddAsync(
            OrderAuditEntry.Create(
                command.TenantId,
                order.Id,
                command.ActorId,
                AuditAction.RecordPlacementEvidence,
                command.EvidenceReference.Trim(),
                now),
            cancellationToken).ConfigureAwait(false);
        var row = await work.FindOneAsync(candidate => candidate.OrderId == order.Id, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            await work.AddAsync(
                PlacementProbeWork.Create(command.TenantId, order.Id, order.EvidenceRevision, command.PlacementId, now, now),
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            row.ResetForEvidence(order.EvidenceRevision, command.PlacementId, now, now);
        }

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
