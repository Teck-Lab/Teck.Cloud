using Ardalis.Specification;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Reconciliation;
using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.ApplyPaymentSnapshot.V1;

/// <summary>Identifies the pending renewal check for one order, excluding the job currently being settled.</summary>
public sealed class PendingRenewalJobSpecification : SingleResultSpecification<ReconciliationJob>
{
    /// <summary>Initializes a new instance of the <see cref="PendingRenewalJobSpecification"/> class.</summary>
    /// <param name="tenantId">The validated tenant id.</param>
    /// <param name="orderId">The owning order id.</param>
    /// <param name="currentJobId">The payment job whose completion must not reuse itself as its own renewal.</param>
    public PendingRenewalJobSpecification(string tenantId, Guid orderId, Guid currentJobId)
    {
        _ = Query.Where(candidate => candidate.TenantId == tenantId
            && candidate.OrderId == orderId
            && candidate.Kind == ReconciliationKind.PaymentRenewalCheck
            && candidate.Status == ReconciliationStatus.Pending
            && candidate.Id != currentJobId);
    }
}
