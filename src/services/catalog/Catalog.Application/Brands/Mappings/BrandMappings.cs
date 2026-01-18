using Catalog.Application.Brands.Features.Responses;
using Catalog.Application.Brands.ReadModels;
using Catalog.Domain.Entities.BrandAggregate;
using Riok.Mapperly.Abstractions;
using SharedKernel.Core.Pagination;

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
        /// <returns>A BrandResponse</returns>
        public static partial BrandResponse BrandToBrandResponse(Brand brand);

        /// <summary>
        /// Maps a paged list of Brand entities to a paged list of BrandResponse DTOs.
        /// </summary>
        /// <param name="brands">The paginated brand list.</param>
        /// <returns>A paged list of BrandResponse DTOs.</returns>
        public static partial PagedList<BrandResponse> PagedBrandToPagedBrandResponse(PagedList<Brand> brands);

        /// <summary>
        /// Maps a BrandReadModel to a BrandResponse.
        /// </summary>
        /// <param name="brand">The brand read model.</param>
        /// <returns>A BrandResponse.</returns>
        internal static partial BrandResponse BrandReadModelToBrandResponse(BrandReadModel brand);

        /// <summary>
        /// Maps a list of BrandReadModel entities to a list of BrandResponse DTOs.
        /// </summary>
        /// <param name="brands">The list of brand read models.</param>
        /// <returns>A list of BrandResponse DTOs.</returns>
        internal static partial List<BrandResponse> BrandReadModelsToResponses(List<BrandReadModel> brands);
    }
}
