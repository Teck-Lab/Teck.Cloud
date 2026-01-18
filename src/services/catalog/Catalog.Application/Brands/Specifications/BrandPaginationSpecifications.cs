using Ardalis.Specification;
using Catalog.Application.Brands.ReadModels;

namespace Catalog.Application.Brands.Specifications
{
    /// <summary>
    /// Specification for paginated brand queries with optional keyword filtering.
    /// </summary>
    public sealed class BrandPaginationSpecification : Specification<BrandReadModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BrandPaginationSpecification"/> class.
        /// </summary>
        /// <param name="page">The page number (1-based).</param>
        /// <param name="pageSize">The page size.</param>
        /// <param name="keyword">Optional keyword for filtering.</param>
        public BrandPaginationSpecification(int page, int pageSize, string? keyword)
        {
            // Apply keyword filter if provided
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                Query.Where(brand => brand.Name.Contains(keyword) ||
                               (brand.Description != null && brand.Description.Contains(keyword)));
            }

            // Add ordering
            Query.OrderBy(brand => brand.Name);

            // Apply pagination
            Query.Skip((page - 1) * pageSize).Take(pageSize);
        }
    }

    /// <summary>
    /// Specification for counting total brands matching a keyword filter.
    /// </summary>
    public sealed class BrandCountSpecification : Specification<BrandReadModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BrandCountSpecification"/> class.
        /// </summary>
        /// <param name="keyword">Optional keyword for filtering.</param>
        public BrandCountSpecification(string? keyword)
        {
            // Apply keyword filter if provided
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                Query.Where(brand => brand.Name.Contains(keyword) ||
                               (brand.Description != null && brand.Description.Contains(keyword)));
            }
        }
    }
}
