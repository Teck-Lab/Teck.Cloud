using Ardalis.Specification;

namespace Catalog.Domain.Entities.CategoryAggregate.Specifications
{
    /// <summary>
    /// Specification to retrieve a category by its ID.
    /// </summary>
    public sealed class CategoryByIdSpecification : Specification<Category>, ISingleResultSpecification<Category>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CategoryByIdSpecification"/> class.
        /// </summary>
        /// <param name="id">The category ID.</param>
        public CategoryByIdSpecification(Guid id)
        {
            Query.Where(category => category.Id == id);
        }
    }
}
