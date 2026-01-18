using Ardalis.Specification;

namespace Catalog.Domain.Entities.CategoryAggregate.Specifications
{
    /// <summary>
    /// Specification to retrieve a category by its ID.
    /// </summary>
    public sealed class CategoriesByIdsSpecification : Specification<Category>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CategoriesByIdsSpecification"/> class.
        /// </summary>
        /// <param name="ids">The category IDs.</param>
        public CategoriesByIdsSpecification(IReadOnlyCollection<Guid> ids)
        {
            Query.Where(category => ids.Contains(category.Id));
        }
    }
}
