using Ardalis.Specification;

namespace Catalog.Domain.Entities.BrandAggregate.Specifications
{
    /// <summary>
    /// Specification to retrieve brands by name (exact match or contains).
    /// </summary>
    public sealed class BrandByNameSpecification : Specification<Brand>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BrandByNameSpecification"/> class for exact name match.
        /// </summary>
        /// <param name="name">The exact brand name to match.</param>
        /// <param name="useExactMatch">Whether to use exact matching (default: true).</param>
        public BrandByNameSpecification(string name, bool useExactMatch = true)
        {
            if (useExactMatch)
            {
                Query.Where(brand => brand.Name == name);
            }
            else
            {
                // Case-insensitive contains
                Query.Where(brand => brand.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
