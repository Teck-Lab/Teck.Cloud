using SharedKernel.Core.Pagination;

namespace Catalog.Application.Brands.Features.GetPaginatedBrands.V1
{
    /// <summary>
    /// The get paginated brands request.
    /// </summary>
    public class GetPaginatedBrandsRequest : PaginationParameters
    {
        /// <summary>
        /// Gets or sets the keyword.
        /// </summary>
        public string? Keyword { get; set; }
    }
}
