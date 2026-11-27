using ErrorOr;
using SharedKernel.Core.Database;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Reconciliation;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.CheckoutWork.V1;

/// <summary>Already-satisfied provider work completion handler.</summary>
public static class CompleteReconciliationJobHandler
{
    /// <summary>Completes the job without touching order facts.</summary>
    /// <param name="command">The command.</param>
    /// <param name="jobs">The tracked job repository.</param>
    /// <param name="unitOfWork">The save boundary.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>True when completed.</returns>
    public static Task<ErrorOr<bool>> HandleAsync(
        CompleteReconciliationJobCommand command,
        IGenericWriteRepository<ReconciliationJob, Guid> jobs,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CompleteAsync(command, jobs, unitOfWork, clock, cancellationToken);
    }

    private static async Task<ErrorOr<bool>> CompleteAsync(
        CompleteReconciliationJobCommand command,
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

        if (!job.HasLiveLease(command.LeaseToken, now)
            || job.OrderId != command.OrderId
            || !string.Equals(job.TenantId, command.TenantId, StringComparison.Ordinal))
        {
            return false;
        }

        job.Complete(now);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
