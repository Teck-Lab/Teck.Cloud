using ErrorOr;
using SharedKernel.Core.Database;
using Teck.Cloud.Commerce.Orders.Application.Orders.Entitlements;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.PlacementProbe;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.ProviderEvent;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Reconciliation;
using Teck.Cloud.Commerce.Orders.Domain.Common;
using Wolverine;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.ApplyPaymentSnapshot.V1;

/// <summary>Authoritative payment application handler.</summary>
public static class ApplyPaymentSnapshotHandler
{
    private const int DefaultRetrySeconds = 300;
    private const int MaximumReceipts = 50;

    /// <summary>Applies provider state, recomputes entitlement, schedules renewal and settles observed receipts.</summary>
    /// <param name="command">The command.</param>
    /// <param name="orders">The tracked order repository.</param>
    /// <param name="jobs">The tracked job repository.</param>
    /// <param name="receipts">The tracked provider-receipt repository.</param>
    /// <param name="work">The tracked probe-work repository.</param>
    /// <param name="unitOfWork">The save boundary.</param>
    /// <param name="bus">The message bus.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>True when the authoritative observation was accepted and persisted.</returns>
    public static Task<ErrorOr<bool>> HandleAsync(
        ApplyPaymentSnapshotCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        IGenericWriteRepository<ProviderEventReceipt, Guid> receipts,
        IGenericWriteRepository<PlacementProbeWork, Guid> work,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return ApplySnapshotAsync(command, orders, jobs, receipts, work, unitOfWork, bus, clock, cancellationToken);
    }

    private static bool BindsStoredFacts(Order order, BillingReconciliation snapshot) =>
        (order.ProviderSubscriptionId is not { } boundSubscription
            || snapshot.ProviderSubscriptionId is not { } providerSubscription
            || string.Equals(boundSubscription, providerSubscription, StringComparison.Ordinal))
        && (order.CheckoutProviderSessionId is not { } boundSession
            || snapshot.ProviderSessionId is not { } providerSession
            || string.Equals(boundSession, providerSession, StringComparison.Ordinal));

    private static bool IsVerifiablePending(ApplyPaymentSnapshotCommand command, Order order) =>
        string.IsNullOrWhiteSpace(command.Snapshot.FailureCode)
        && !string.IsNullOrWhiteSpace(command.Snapshot.ProviderSessionId)
        && order.CheckoutProviderSessionId is { } boundSession
        && string.Equals(boundSession, command.Snapshot.ProviderSessionId, StringComparison.Ordinal);

    private static DateTimeOffset NextDueAt(DateTimeOffset now, int retrySeconds) =>
        now.AddSeconds(retrySeconds > 0 ? retrySeconds : DefaultRetrySeconds);

    private static async Task<ErrorOr<bool>> ApplySnapshotAsync(
        ApplyPaymentSnapshotCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        IGenericWriteRepository<ProviderEventReceipt, Guid> receipts,
        IGenericWriteRepository<PlacementProbeWork, Guid> work,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var job = await jobs.LoadForUpdateAsync(command.JobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return false;
        }

        var order = await orders.LoadForUpdateAsync(command.OrderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return false;
        }

        // The lease clock is read after the tracked rows are loaded so an expired
        // lease is never treated as live, and the job's lease token is a mapped
        // concurrency token so a stale owner cannot commit over a reclaimed row.
        var now = clock.GetUtcNow();
        var validClaim = job.HasLiveLease(command.LeaseToken, now)
            && job.OrderId == command.OrderId
            && job.Status == ReconciliationStatus.Pending
            && string.Equals(job.TenantId, command.TenantId, StringComparison.Ordinal)
            && job.Kind is ReconciliationKind.PaymentVerification or ReconciliationKind.PaymentRenewalCheck
            && BindsStoredFacts(order, command.Snapshot);
        return validClaim
            ? await ApplyObservationAsync(command, order, job, jobs, receipts, work, unitOfWork, bus, now, cancellationToken).ConfigureAwait(false)
            : await ErrorOrFactory.FromAsync(value: false).ConfigureAwait(false);
    }

    private static Task<ErrorOr<bool>> ApplyObservationAsync(
        ApplyPaymentSnapshotCommand command,
        Order order,
        ReconciliationJob job,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        IGenericWriteRepository<ProviderEventReceipt, Guid> receipts,
        IGenericWriteRepository<PlacementProbeWork, Guid> work,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        command.Snapshot.Status == BillingEvidenceStatus.Pending
            ? ApplyPendingAsync(command, order, job, receipts, unitOfWork, now, cancellationToken)
            : ApplyAuthoritativeAsync(command, order, job, jobs, receipts, work, unitOfWork, bus, now, cancellationToken);

    private static async Task<ErrorOr<bool>> ApplyPendingAsync(
        ApplyPaymentSnapshotCommand command,
        Order order,
        ReconciliationJob job,
        IGenericWriteRepository<ProviderEventReceipt, Guid> receipts,
        IUnitOfWork unitOfWork,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!IsVerifiablePending(command, order))
        {
            return false;
        }

        // A failure-free re-fetch that only proves "not paid yet" must never
        // write null paid-through or touch license state; it only proves the
        // bound receipts and reschedules durable polling.
        if (job.Kind == ReconciliationKind.PaymentVerification)
        {
            await CompleteReceiptsAsync(command, receipts, now, cancellationToken).ConfigureAwait(false);
        }

        _ = job.Reschedule(NextDueAt(now, command.RetrySeconds), now);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static async Task<ErrorOr<bool>> ApplyAuthoritativeAsync(
        ApplyPaymentSnapshotCommand command,
        Order order,
        ReconciliationJob job,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        IGenericWriteRepository<ProviderEventReceipt, Guid> receipts,
        IGenericWriteRepository<PlacementProbeWork, Guid> work,
        IUnitOfWork unitOfWork,
        IMessageBus bus,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var snapshot = command.Snapshot;

        if (snapshot.Status is not (BillingEvidenceStatus.Paid
            or BillingEvidenceStatus.Refunded
            or BillingEvidenceStatus.Disputed
            or BillingEvidenceStatus.Canceled))
        {
            return false;
        }

        var paidThrough = snapshot.Status == BillingEvidenceStatus.Paid ? snapshot.PaidPeriodEnd : null;
        var previous = order.DesiredRevision;
        _ = order.ApplyPayment(
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

        return job.Kind == ReconciliationKind.PaymentRenewalCheck
            ? await ApplyRenewalAsync(command, order, job, work, paidThrough, unitOfWork, now, cancellationToken).ConfigureAwait(false)
            : await SettleVerificationAsync(command, order, job, jobs, receipts, work, paidThrough, unitOfWork, now, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ErrorOr<bool>> SettleVerificationAsync(
        ApplyPaymentSnapshotCommand command,
        Order order,
        ReconciliationJob job,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        IGenericWriteRepository<ProviderEventReceipt, Guid> receipts,
        IGenericWriteRepository<PlacementProbeWork, Guid> work,
        DateTimeOffset? paidThrough,
        IUnitOfWork unitOfWork,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await CompleteReceiptsAsync(command, receipts, now, cancellationToken).ConfigureAwait(false);

        // Rescheduling and saving first acquires the pending payment job row lock.
        // Intake takes the same lock before attaching a receipt, so the scan below
        // cannot miss a receipt that arrived after this command was built.
        _ = job.Reschedule(NextDueAt(now, command.RetrySeconds), now);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (paidThrough is { } periodEnd)
        {
            await RequeueProbeAsync(command, order, work, now, cancellationToken).ConfigureAwait(false);
            await EnsureRenewalAsync(command.TenantId, order.Id, job.Id, periodEnd, jobs, now, cancellationToken)
                .ConfigureAwait(false);
        }

        if (await HasRemainingActionableReceiptsAsync(command, receipts, cancellationToken).ConfigureAwait(false))
        {
            // The job stays pending and due; this save persists renewal side effects.
            _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        job.Complete(now);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // An unchanged paid snapshot is still a durable authoritative outcome; never
        // report it as a mismatched binding, which would fail its bound receipts.
        return true;
    }

    private static async Task<ErrorOr<bool>> ApplyRenewalAsync(
        ApplyPaymentSnapshotCommand command,
        Order order,
        ReconciliationJob job,
        IGenericWriteRepository<PlacementProbeWork, Guid> work,
        DateTimeOffset? paidThrough,
        IUnitOfWork unitOfWork,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (paidThrough is { } periodEnd)
        {
            await RequeueProbeAsync(command, order, work, now, cancellationToken).ConfigureAwait(false);
            _ = job.Reschedule(periodEnd > now ? periodEnd : NextDueAt(now, command.RetrySeconds), now);
        }
        else
        {
            job.Complete(now);
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static async Task CompleteReceiptsAsync(
        ApplyPaymentSnapshotCommand command,
        IGenericWriteRepository<ProviderEventReceipt, Guid> receipts,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (var receiptId in command.ReceiptIds.Distinct().Take(MaximumReceipts))
        {
            var receipt = await receipts.LoadForUpdateAsync(receiptId, cancellationToken).ConfigureAwait(false);
            if (receipt is null
                || receipt.OrderId != command.OrderId
                || !string.Equals(receipt.TenantId, command.TenantId, StringComparison.Ordinal)
                || receipt.Status is not (ProviderReceiptStatus.Received or ProviderReceiptStatus.Processing))
            {
                continue;
            }

            receipt.MarkCompleted(now);
        }
    }

    private static Task<bool> HasRemainingActionableReceiptsAsync(
        ApplyPaymentSnapshotCommand command,
        IGenericWriteRepository<ProviderEventReceipt, Guid> receipts,
        CancellationToken cancellationToken) =>
        receipts.ExistsAsync(
            candidate => candidate.TenantId == command.TenantId
                && candidate.OrderId == command.OrderId
                && (candidate.Status == ProviderReceiptStatus.Received
                    || candidate.Status == ProviderReceiptStatus.Processing),
            cancellationToken);

    private static async Task RequeueProbeAsync(
        ApplyPaymentSnapshotCommand command,
        Order order,
        IGenericWriteRepository<PlacementProbeWork, Guid> work,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var parked = await work.LoadForUpdateAsync(
            new ProbeWorkByOrderSpecification(command.TenantId, order.Id),
            cancellationToken).ConfigureAwait(false);
        _ = parked?.Requeue(now, now);
    }

    private static async Task EnsureRenewalAsync(
        string tenantId,
        Guid orderId,
        Guid currentJobId,
        DateTimeOffset dueAt,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var renewal = await jobs.LoadForUpdateAsync(
            new PendingRenewalJobSpecification(tenantId, orderId, currentJobId),
            cancellationToken).ConfigureAwait(false);
        if (renewal is null)
        {
            await jobs.AddAsync(
                ReconciliationJob.Create(tenantId, orderId, ReconciliationKind.PaymentRenewalCheck, dueAt, now),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (renewal.LeaseUntil is { } until && until > now)
        {
            // Another claim owns the renewal lease; never clear it.
            return;
        }

        _ = renewal.Reschedule(dueAt, now);
    }
}
