// <copyright file="BrandMappings.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Brands.Features.CreateBrand.V1;
using Catalog.Application.Brands.Features.GetBrandById.V1;
using Catalog.Application.Brands.Features.GetPaginatedBrands.V1;
using Catalog.Application.Brands.Features.UpdateBrand.V1;
using Catalog.Application.Brands.ReadModels;
using Catalog.Domain.Entities.BrandAggregate;
using Riok.Mapperly.Abstractions;

namespace Catalog.Application.Brands.Mappings
{
    /// <summary>
    /// The brand mappings.
    /// </summary>
    [Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
    public static partial class BrandMapper
    {
        /// <summary>
        /// Brand converts to brand response.
        /// </summary>
        /// <param name="brand">The brand.</param>
        /// <returns>A create brand response.</returns>
        public static partial CreateBrandResponse BrandToCreateBrandResponse(Brand brand);

        /// <summary>
        /// Maps a domain Brand to an update brand response.
        /// </summary>
        /// <param name="brand">The brand domain model.</param>
        /// <returns>An update brand response.</returns>
        public static partial UpdateBrandResponse BrandToUpdateBrandResponse(Brand brand);

        /// <summary>
        /// Maps a BrandReadModel to a get-by-id brand response.
        /// </summary>
        /// <param name="brand">The brand read model.</param>
        /// <returns>A get-by-id brand response.</returns>
        internal static partial GetByIdBrandResponse BrandReadModelToGetByIdBrandResponse(BrandReadModel brand);

        /// <summary>
        /// Maps a BrandReadModel to a get-paginated-brands response.
        /// </summary>
        /// <param name="brand">The brand read model.</param>
        /// <returns>A get-paginated-brands response.</returns>
        internal static partial GetPaginatedBrandsResponse BrandReadModelToGetPaginatedBrandsResponse(BrandReadModel brand);
    }
}
