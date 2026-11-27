using Ardalis.Specification;
using Teck.Cloud.Commerce.Licensing.Domain.Aggregates.LicenseGenerations;

namespace Teck.Cloud.Commerce.Licensing.Application.Entitlements.Specifications;

/// <summary>Loads the single tracked generation fence for an exact binding.</summary>
public sealed class LicenseGenerationByBindingSpecification : SingleResultSpecification<LicenseGeneration>
{
    /// <summary>Initializes a new instance of the <see cref="LicenseGenerationByBindingSpecification"/> class.</summary>
    /// <param name="tenantId">The canonical organization UUID.</param>
    /// <param name="orderId">The bound order.</param>
    /// <param name="subscriptionId">The bound subscription.</param>
    public LicenseGenerationByBindingSpecification(string tenantId, Guid orderId, Guid subscriptionId)
    {
        _ = Query.Where(generation => generation.TenantId == tenantId
            && generation.OrderId == orderId
            && generation.SubscriptionId == subscriptionId);
    }
}
