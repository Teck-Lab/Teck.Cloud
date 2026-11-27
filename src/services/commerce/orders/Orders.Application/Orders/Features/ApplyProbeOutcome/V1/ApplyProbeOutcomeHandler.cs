using ErrorOr;
using SharedKernel.Core.Database;
using Teck.Cloud.Commerce.Orders.Application.Orders.Entitlements;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.PlacementProbe;
using Teck.Cloud.Commerce.Orders.Domain.Common;
using Wolverine;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.ApplyProbeOutcome.V1;

/// <summary>Generation-safe probe outcome handler.</summary>
public static class ApplyProbeOutcomeHandler
{
    /// <summary>Applies the outcome only when the lease, evidence and gates are still current.</summary>
    /// <param name="command">The command.</param>
    /// <param name="orders">The tracked order repository.</param>
    /// <param name="work">The tracked probe-work repository.</param>
    /// <param name="unitOfWork">The save boundary.</param>
    /// <param name="bus">The message bus.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>True when applied; stale or superseded outcomes are discarded.</returns>
    public static Task<ErrorOr<bool>> HandleAsync(
        ApplyProbeOutcomeCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IGenericWriteRepository<PlacementProbeWork, Guid> work,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return ApplyOutcomeAsync(command, orders, work, unitOfWork, bus, clock, cancellationToken);
    }

    private static async Task<ErrorOr<bool>> ApplyOutcomeAsync(
        ApplyProbeOutcomeCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IGenericWriteRepository<PlacementProbeWork, Guid> work,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var row = await work.LoadForUpdateAsync(command.WorkId, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return false;
        }

        if (!row.HasLiveLease(command.LeaseToken, now)
            || row.OrderId != command.OrderId
            || row.EvidenceRevision != command.EvidenceRevision
            || !string.Equals(row.PlacementId, command.PlacementId, StringComparison.Ordinal))
        {
            return false;
        }

        var order = await orders.LoadForUpdateAsync(command.OrderId, cancellationToken).ConfigureAwait(false);
        if (order is null
            || order.EvidenceRevision != command.EvidenceRevision
            || !string.Equals(order.EvidencePlacementId, command.PlacementId, StringComparison.Ordinal)
            || !order.IsPaidAt(now)
            || order.ApprovalStatus != ApprovalStatus.Approved)
        {
            row.ReleaseLease(now);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        var previous = order.DesiredRevision;
        var applied = command.Kind switch
        {
            ProbeOutcomeKind.Verified => order.RecordProbeSuccess(command.EvidenceRevision, command.PlacementId, command.ObservedAt, now),
            ProbeOutcomeKind.Unavailable => order.RecordProbeFailure(command.EvidenceRevision, command.PlacementId, FulfillmentFailureCode.ProbeUnavailable, command.ObservedAt, now),
            ProbeOutcomeKind.BindingMismatch => order.RecordProbeFailure(command.EvidenceRevision, command.PlacementId, FulfillmentFailureCode.BindingMismatch, command.ObservedAt, now),
            ProbeOutcomeKind.CapabilityUnavailable => order.RecordProbeFailure(command.EvidenceRevision, command.PlacementId, FulfillmentFailureCode.CapabilityUnavailable, command.ObservedAt, now),
            _ => false,
        };
        if (!applied)
        {
            row.ReleaseLease(now);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        CompleteWork(row, command, now);
        _ = order.RecomputeDesired(now);
        await LicenseDesiredPublisher.PublishIfChangedAsync(order, previous, bus, now, cancellationToken).ConfigureAwait(false);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static void CompleteWork(PlacementProbeWork row, ApplyProbeOutcomeCommand command, DateTimeOffset now)
    {
        switch (command.Kind)
        {
            case ProbeOutcomeKind.Verified:
                row.CompleteSuccess(command.NextDueAt, now);
                break;
            case ProbeOutcomeKind.Unavailable:
                row.CompleteTransientFailure(command.NextDueAt, now);
                break;
            case ProbeOutcomeKind.BindingMismatch:
                row.CompleteTerminalFailure(FulfillmentFailureCode.BindingMismatch, now);
                break;
            case ProbeOutcomeKind.CapabilityUnavailable:
                row.CompleteTerminalFailure(FulfillmentFailureCode.CapabilityUnavailable, now);
                break;
            default:
                throw new InvalidOperationException($"Unsupported probe outcome kind '{command.Kind}'.");
        }
    }
}
