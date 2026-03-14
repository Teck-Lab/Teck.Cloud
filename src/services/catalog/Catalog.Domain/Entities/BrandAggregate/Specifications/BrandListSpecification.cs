// <copyright file="BrandListSpecification.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Ardalis.Specification;

namespace Catalog.Domain.Entities.BrandAggregate.Specifications
{
    /// <summary>
    /// Defines ordering mode for brand listing.
    /// </summary>
    public enum BrandOrdering
    {
        /// <summary>
        /// Order by brand name.
        /// </summary>
        ByName,

        /// <summary>
        /// Order by creation date descending.
        /// </summary>
        ByCreationDate,
    }

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
        /// <param name="ordering">The ordering mode.</param>
        public BrandListSpecification(
            int? skip = null,
            int? take = null,
            string nameFilter = "",
            BrandOrdering ordering = BrandOrdering.ByName)
        {
            this.ApplyNameFilter(nameFilter);
            this.ApplyOrdering(ordering);
            this.ApplyPaging(skip, take);
        }

        private void ApplyNameFilter(string nameFilter)
        {
            if (!string.IsNullOrWhiteSpace(nameFilter))
            {
                this.Query.Where(brand => brand.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase));
            }
        }

        private void ApplyOrdering(BrandOrdering ordering)
        {
            if (ordering == BrandOrdering.ByCreationDate)
            {
                this.Query.OrderByDescending(brand => brand.CreatedAt);
            }
            else
            {
                this.Query.OrderBy(brand => brand.Name);
            }
        }

        private void ApplyPaging(int? skip, int? take)
        {
            if (skip is not null && take is not null)
            {
                this.Query.Skip(skip.Value).Take(take.Value);
            }
            else
            {
                if (take is not null)
                {
                    this.Query.Take(take.Value);
                }
            }
        }
    }
}
