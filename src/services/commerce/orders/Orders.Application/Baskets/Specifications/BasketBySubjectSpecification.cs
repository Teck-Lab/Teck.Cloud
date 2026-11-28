using Ardalis.Specification;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Baskets;

namespace Teck.Cloud.Commerce.Orders.Application.Baskets.Specifications;

/// <summary>Identifies the single basket owned by one tenant and subject.</summary>
public sealed class BasketBySubjectSpecification : SingleResultSpecification<Basket>
{
    /// <summary>Initializes a new instance of the <see cref="BasketBySubjectSpecification"/> class.</summary>
    /// <param name="tenantId">The validated tenant id.</param>
    /// <param name="subjectId">The authenticated subject.</param>
    public BasketBySubjectSpecification(string tenantId, string subjectId)
    {
        _ = Query.Where(basket => basket.TenantId == tenantId && basket.SubjectId == subjectId);
    }
}
