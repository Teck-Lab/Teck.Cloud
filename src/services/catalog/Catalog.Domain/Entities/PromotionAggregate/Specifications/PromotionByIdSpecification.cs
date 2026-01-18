using Ardalis.Specification;

namespace Catalog.Domain.Entities.PromotionAggregate.Specifications
{
    /// <summary>
    /// Specification to retrieve a promotion by its ID.
    /// </summary>
    public sealed class PromotionByIdSpecification : Specification<Promotion>, ISingleResultSpecification<Promotion>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PromotionByIdSpecification"/> class.
        /// </summary>
        /// <param name="id">The promotion ID.</param>
        /// <param name="includeProducts">Whether to include related products in the query.</param>
        public PromotionByIdSpecification(Guid id, bool includeProducts = false)
        {
            Query.Where(promotion => promotion.Id == id);

            if (includeProducts)
            {
                Query.Include(promotion => promotion.Products);
            }
        }
    }
}
