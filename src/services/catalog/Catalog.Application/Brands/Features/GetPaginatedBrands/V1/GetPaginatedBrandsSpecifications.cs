// <copyright file="GetPaginatedBrandsSpecifications.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Ardalis.Specification;
using Catalog.Application.Brands.ReadModels;

namespace Catalog.Application.Brands.Features.GetPaginatedBrands.V1
{
    /// <summary>
    /// Specification for paginated brand queries with optional keyword filtering.
    /// </summary>
    public sealed class GetPaginatedBrandsSpecification : Specification<BrandReadModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GetPaginatedBrandsSpecification"/> class.
        /// </summary>
        /// <param name="page">The page number (1-based).</param>
        /// <param name="pageSize">The page size.</param>
        /// <param name="keyword">Optional keyword for filtering.</param>
        public GetPaginatedBrandsSpecification(int page, int pageSize, string? keyword)
        {
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                this.Query.Where(brand => brand.Name.Contains(keyword) ||
                               (brand.Description != null && brand.Description.Contains(keyword)));
            }

            this.Query.OrderBy(brand => brand.Name);
            this.Query.Skip((page - 1) * pageSize).Take(pageSize);
        }
    }

    /// <summary>
    /// Specification for counting total brands matching a keyword filter.
    /// </summary>
    public sealed class GetPaginatedBrandsCountSpecification : Specification<BrandReadModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GetPaginatedBrandsCountSpecification"/> class.
        /// </summary>
        /// <param name="keyword">Optional keyword for filtering.</param>
        public GetPaginatedBrandsCountSpecification(string? keyword)
        {
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                this.Query.Where(brand => brand.Name.Contains(keyword) ||
                               (brand.Description != null && brand.Description.Contains(keyword)));
            }
        }
    }
}
