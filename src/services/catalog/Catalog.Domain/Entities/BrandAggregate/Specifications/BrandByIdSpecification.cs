using Ardalis.Specification;

namespace Catalog.Domain.Entities.BrandAggregate.Specifications
{
    /// <summary>
    /// Specification to retrieve a brand by its ID.
    /// </summary>
    public sealed class BrandByIdSpecification : Specification<Brand>, ISingleResultSpecification<Brand>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BrandByIdSpecification"/> class.
        /// </summary>
        /// <param name="id">The brand ID.</param>
        public BrandByIdSpecification(Guid id)
        {
            Query.Where(brand => brand.Id == id);
        }
    }
}
