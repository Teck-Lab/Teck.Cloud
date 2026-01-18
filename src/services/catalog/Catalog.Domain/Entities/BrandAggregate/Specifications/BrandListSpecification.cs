using Ardalis.Specification;

namespace Catalog.Domain.Entities.BrandAggregate.Specifications
{
    /// <summary>
    /// Specification to retrieve brands with optional paging, sorting and filtering.
    /// </summary>
    public sealed class BrandListSpecification : Specification<Brand>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BrandListSpecification"/> class.
        /// </summary>
        /// <param name="skip">The number of items to skip for paging.</param>
        /// <param name="take">The number of items to take for paging.</param>
        /// <param name="nameFilter">Optional name filter.</param>
        /// <param name="orderByCreationDate">Whether to order by creation date (newest first).</param>
        public BrandListSpecification(
            int? skip = null,
            int? take = null,
            string nameFilter = "",
            bool orderByCreationDate = false)
        {
            // Apply name filter if provided
            if (!string.IsNullOrWhiteSpace(nameFilter))
            {
                Query.Where(brand => brand.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Apply ordering
            if (orderByCreationDate)
            {
                Query.OrderByDescending(brand => brand.CreatedAt);
            }
            else
            {
                Query.OrderBy(brand => brand.Name);
            }

            // Apply paging if provided
            if (skip.HasValue && take.HasValue)
            {
                Query.Skip(skip.Value).Take(take.Value);
            }
            else if (take.HasValue)
            {
                Query.Take(take.Value);
            }
        }
    }
}
