using ErrorOr;
using SharedKernel.Core.Database;
using Teck.Cloud.Commerce.Orders.Application.Orders.Entitlements;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.PlacementProbe;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Reconciliation;
using Teck.Cloud.Commerce.Orders.Domain.Common;
using Wolverine;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.ApplyPaymentSnapshot.V1;

/// <summary>Authoritative payment application handler.</summary>
public static class ApplyPaymentSnapshotHandler
{
    /// <summary>Applies provider state, recomputes entitlement and schedules renewal.</summary>
    /// <param name="command">The command.</param>
    /// <param name="orders">The tracked order repository.</param>
    /// <param name="jobs">The tracked job repository.</param>
    /// <param name="work">The tracked probe-work repository.</param>
    /// <param name="unitOfWork">The save boundary.</param>
    /// <param name="bus">The message bus.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>True when an authoritative state was applied.</returns>
    public static Task<ErrorOr<bool>> HandleAsync(
        ApplyPaymentSnapshotCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        IGenericWriteRepository<PlacementProbeWork, Guid> work,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return ApplySnapshotAsync(command, orders, jobs, work, unitOfWork, bus, clock, cancellationToken);
    }

    private static async Task<ErrorOr<bool>> ApplySnapshotAsync(
        ApplyPaymentSnapshotCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        IGenericWriteRepository<PlacementProbeWork, Guid> work,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var job = await jobs.LoadForUpdateAsync(command.JobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return false;
        }

        if (!job.HasLiveLease(command.LeaseToken, now) || job.OrderId != command.OrderId
            || !string.Equals(job.TenantId, command.TenantId, StringComparison.Ordinal))
        {
            return false;
        }

        var order = await orders.LoadForUpdateAsync(command.OrderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return false;
        }

        var snapshot = command.Snapshot;
        if (order.ProviderSubscriptionId is { } boundSubscription
            && snapshot.ProviderSubscriptionId is { } providerSubscription
            && !string.Equals(boundSubscription, providerSubscription, StringComparison.Ordinal))
        {
            return false;
        }

        if (order.CheckoutProviderSessionId is { } boundSession
            && snapshot.ProviderSessionId is { } providerSession
            && !string.Equals(boundSession, providerSession, StringComparison.Ordinal))
        {
            return false;
        }

        var paidThrough = snapshot.Status == BillingEvidenceStatus.Paid ? snapshot.PaidPeriodEnd : null;
        var previous = order.DesiredRevision;
        var changed = order.ApplyPayment(
            paidThrough,
            snapshot.ProviderSessionId,
            snapshot.ProviderSubscriptionId,
            snapshot.InvoiceId,
            snapshot.ChargeId,
            snapshot.PaymentIntentId,
            snapshot.Mode.ToString().ToLowerInvariant(),
            snapshot.AccountId,
            priceKey: null,
            now);
        _ = order.RecomputeDesired(now);
        await LicenseDesiredPublisher.PublishIfChangedAsync(order, previous, bus, now, cancellationToken).ConfigureAwait(false);
        job.Complete(now);
        if (paidThrough is { } periodEnd)
        {
            await ScheduleRenewalAsync(command, order, work, jobs, periodEnd, now, cancellationToken).ConfigureAwait(false);
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return changed;
    }

    private static async Task ScheduleRenewalAsync(
        ApplyPaymentSnapshotCommand command,
        Order order,
        IGenericWriteRepository<PlacementProbeWork, Guid> work,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        DateTimeOffset periodEnd,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var parked = await work.FindOneAsync(candidate => candidate.OrderId == order.Id, cancellationToken).ConfigureAwait(false);
        _ = parked?.Requeue(now, now);
        await EnsureRenewalAsync(command.TenantId, order.Id, periodEnd, jobs, now, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureRenewalAsync(
        string tenantId,
        Guid orderId,
        DateTimeOffset dueAt,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var renewal = await jobs.FindOneAsync(
            candidate => candidate.OrderId == orderId
                && candidate.Kind == ReconciliationKind.PaymentRenewalCheck
                && candidate.Status == ReconciliationStatus.Pending,
            cancellationToken).ConfigureAwait(false);
        if (renewal is null)
        {
            await jobs.AddAsync(
                ReconciliationJob.Create(tenantId, orderId, ReconciliationKind.PaymentRenewalCheck, dueAt, now),
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _ = renewal.Reschedule(dueAt, now);
        }
    }
}
