using Ardalis.Specification;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.PlacementProbe;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.ApplyPaymentSnapshot.V1;

/// <summary>Identifies the single probe-work row for one tenant and order.</summary>
public sealed class ProbeWorkByOrderSpecification : SingleResultSpecification<PlacementProbeWork>
{
    /// <summary>Initializes a new instance of the <see cref="ProbeWorkByOrderSpecification"/> class.</summary>
    /// <param name="tenantId">The validated tenant id.</param>
    /// <param name="orderId">The owning order id.</param>
    public ProbeWorkByOrderSpecification(string tenantId, Guid orderId)
    {
        _ = Query.Where(candidate => candidate.TenantId == tenantId && candidate.OrderId == orderId);
    }
}
