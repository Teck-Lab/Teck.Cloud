using Catalog.Application.Brands.Features.Responses;
using Catalog.Application.Brands.Mappings;
using Catalog.Application.Brands.Repositories;
using Catalog.Application.Brands.Specifications;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Pagination;

namespace Catalog.Application.Brands.Features.GetPaginatedBrands.V1
{
    /// <summary>
    /// Get paginated brands query.
    /// </summary>
    public sealed record GetPaginatedBrandsQuery(int Page, int Size, string? Keyword) : IQuery<ErrorOr<PagedList<BrandResponse>>>;

    /// <summary>
    /// Get paginated brands query handler.
    /// </summary>
    internal sealed class GetPaginatedBrandsQueryHandler : IQueryHandler<GetPaginatedBrandsQuery, ErrorOr<PagedList<BrandResponse>>>
    {
        /// <summary>
        /// The brand repository.
        /// </summary>
        private readonly IBrandReadRepository _brandRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetPaginatedBrandsQueryHandler"/> class.
        /// </summary>
        /// <param name="brandRepository">The brand repository.</param>
        public GetPaginatedBrandsQueryHandler(IBrandReadRepository brandRepository)
        {
            _brandRepository = brandRepository;
        }

        /// <summary>
        /// Handle and return a task of a pagedlist of brandresponses.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns><![CDATA[Task<PagedList<BrandResponse>>]]></returns>
        public async ValueTask<ErrorOr<PagedList<BrandResponse>>> Handle(GetPaginatedBrandsQuery request, CancellationToken cancellationToken)
        {
            // Create specifications
            var paginationSpec = new BrandPaginationSpecification(request.Page, request.Size, request.Keyword);
            var countSpec = new BrandCountSpecification(request.Keyword);

            // Get items for current page
            var brands = await _brandRepository.ListAsync(paginationSpec, cancellationToken);

            // Get total count
            var totalCount = await _brandRepository.CountAsync(countSpec, cancellationToken);

            // Map to response objects
            var brandResponses = brands.Select(brand => BrandMapper.BrandReadModelToBrandResponse(brand)).ToList();

            // Create paged list
            return new PagedList<BrandResponse>(
                brandResponses,
                totalCount,
                request.Page,
                request.Size);
        }
    }
}
