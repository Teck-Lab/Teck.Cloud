using Ardalis.Specification;

namespace Catalog.Domain.Entities.ProductPriceTypeAggregate.Specifications
{
    /// <summary>
    /// Specification to retrieve a product price type by its ID.
    /// </summary>
    public sealed class ProductPriceTypeByIdSpecification : Specification<ProductPriceType>, ISingleResultSpecification<ProductPriceType>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProductPriceTypeByIdSpecification"/> class.
        /// </summary>
        /// <param name="id">The product price type ID.</param>
        public ProductPriceTypeByIdSpecification(Guid id)
        {
            Query.Where(priceType => priceType.Id == id);
        }
    }
}
