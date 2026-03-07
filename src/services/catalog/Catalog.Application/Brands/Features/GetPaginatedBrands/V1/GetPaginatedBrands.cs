// <copyright file="GetPaginatedBrands.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Brands.Mappings;
using Catalog.Application.Brands.ReadModels;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Database;
using SharedKernel.Core.Pagination;

namespace Catalog.Application.Brands.Features.GetPaginatedBrands.V1
{
    /// <summary>
    /// Get paginated brands query.
    /// </summary>
    public sealed record GetPaginatedBrandsQuery(int Page, int Size, string? Keyword) : IQuery<ErrorOr<PagedList<GetPaginatedBrandsResponse>>>;

    /// <summary>
    /// Get paginated brands query handler.
    /// </summary>
    internal sealed class GetPaginatedBrandsQueryHandler : IQueryHandler<GetPaginatedBrandsQuery, ErrorOr<PagedList<GetPaginatedBrandsResponse>>>
    {
        /// <summary>
        /// The brand read repository.
        /// </summary>
        private readonly IGenericReadRepository<BrandReadModel, Guid> brandRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetPaginatedBrandsQueryHandler"/> class.
        /// </summary>
        /// <param name="brandRepository">The brand read repository.</param>
        public GetPaginatedBrandsQueryHandler(IGenericReadRepository<BrandReadModel, Guid> brandRepository)
        {
            this.brandRepository = brandRepository;
        }

        /// <summary>
        /// Handle and return a task of a pagedlist of brandresponses.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns><![CDATA[Task<PagedList<GetPaginatedBrandsResponse>>]]></returns>
        public async ValueTask<ErrorOr<PagedList<GetPaginatedBrandsResponse>>> Handle(GetPaginatedBrandsQuery request, CancellationToken cancellationToken)
        {
            // Create specifications
            var paginationSpec = new GetPaginatedBrandsSpecification(request.Page, request.Size, request.Keyword);
            var countSpec = new GetPaginatedBrandsCountSpecification(request.Keyword);

            // Get items for current page
            var brands = await this.brandRepository.ListAsync(paginationSpec, cancellationToken).ConfigureAwait(false);

            // Get total count
            var totalCount = await this.brandRepository.CountAsync(countSpec, cancellationToken).ConfigureAwait(false);

            // Map to response objects
            var brandResponses = brands.Select(brand => BrandMapper.BrandReadModelToGetPaginatedBrandsResponse(brand)).ToList();

            // Create paged list
            return new PagedList<GetPaginatedBrandsResponse>(
                brandResponses,
                totalCount,
                request.Page,
                request.Size);
        }
    }
}
