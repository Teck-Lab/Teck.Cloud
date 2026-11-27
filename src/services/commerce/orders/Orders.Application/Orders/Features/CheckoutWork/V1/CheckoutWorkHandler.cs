using ErrorOr;
using SharedKernel.Core.Database;
using Teck.Cloud.Commerce.Orders.Application.Orders.Billing;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Reconciliation;
using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.CheckoutWork.V1;

/// <summary>Checkout work handlers.</summary>
public static class CheckoutWorkHandler
{
    /// <summary>Persists the immutable verified price snapshot and starts the attempt.</summary>
    /// <param name="command">The command.</param>
    /// <param name="orders">The tracked order repository.</param>
    /// <param name="jobs">The tracked job repository.</param>
    /// <param name="unitOfWork">The save boundary.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>True when the attempt started.</returns>
    public static Task<ErrorOr<bool>> HandleAsync(
        BeginCheckoutAttemptCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return BeginAttemptAsync(command, orders, jobs, unitOfWork, clock, cancellationToken);
    }

    /// <summary>Binds the session and completes the checkout job.</summary>
    /// <param name="command">The command.</param>
    /// <param name="orders">The tracked order repository.</param>
    /// <param name="jobs">The tracked job repository.</param>
    /// <param name="unitOfWork">The save boundary.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>True when applied.</returns>
    public static Task<ErrorOr<bool>> HandleAsync(
        ApplyCheckoutSessionCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return BindSessionAsync(command, orders, jobs, unitOfWork, clock, cancellationToken);
    }

    /// <summary>Records the sanitized failure and releases the order lease.</summary>
    /// <param name="command">The command.</param>
    /// <param name="orders">The tracked order repository.</param>
    /// <param name="jobs">The tracked job repository.</param>
    /// <param name="unitOfWork">The save boundary.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>True when applied.</returns>
    public static Task<ErrorOr<bool>> HandleAsync(
        RecordReconciliationFailureCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RecordFailureAsync(command, orders, jobs, unitOfWork, clock, cancellationToken);
    }

    private static async Task<ErrorOr<bool>> BeginAttemptAsync(
        BeginCheckoutAttemptCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        IUnitOfWork unitOfWork,
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
            || !string.Equals(job.TenantId, command.TenantId, StringComparison.Ordinal)
            || job.Kind != ReconciliationKind.CheckoutSession || job.Status != ReconciliationStatus.Pending)
        {
            return false;
        }

        var order = await orders.LoadForUpdateAsync(command.OrderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return false;
        }

        if (!order.CanCheckoutAt(now))
        {
            return false;
        }

        if (!order.TrySetVerifiedPriceSnapshot(PriceSnapshot.Encode(command.Lines)))
        {
            return false;
        }

        order.BeginCheckoutAttempt(now);
        if (!order.TryBeginCheckoutLease(command.LeaseToken, job.LeaseUntil ?? now.AddSeconds(60), now))
        {
            return false;
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static async Task<ErrorOr<bool>> BindSessionAsync(
        ApplyCheckoutSessionCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        IUnitOfWork unitOfWork,
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
            || !string.Equals(job.TenantId, command.TenantId, StringComparison.Ordinal)
            || job.Kind != ReconciliationKind.CheckoutSession)
        {
            return false;
        }

        var order = await orders.LoadForUpdateAsync(command.OrderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return false;
        }

        var bound = order.ApplyCheckoutSession(
            command.LeaseToken,
            command.SessionId,
            command.CheckoutUrl,
            command.Mode,
            command.AccountId,
            now);
        if (bound.IsError)
        {
            return false;
        }

        job.Complete(now);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static async Task<ErrorOr<bool>> RecordFailureAsync(
        RecordReconciliationFailureCommand command,
        IGenericWriteRepository<Order, Guid> orders,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        IUnitOfWork unitOfWork,
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
        if (order is not null && job.Kind == ReconciliationKind.CheckoutSession)
        {
            order.ReleaseCheckoutLease(command.LeaseToken, command.Category, now);
        }

        if (command.Abandon)
        {
            job.Abandon(command.Category, now);
        }
        else
        {
            job.RecordFailure(command.NextDueAt, command.Category, now);
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
