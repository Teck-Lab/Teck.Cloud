// <copyright file="GetBrandById.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Brands.Mappings;
using Catalog.Application.Brands.ReadModels;
using Catalog.Domain.Entities.BrandAggregate.Errors;
using ErrorOr;
using SharedKernel.Core.Caching;
using SharedKernel.Core.CQRS;

namespace Catalog.Application.Brands.Features.GetBrandById.V1
{
    /// <summary>
    /// Get Brand query.
    /// </summary>
    public sealed record GetBrandByIdQuery(Guid Id) : IQuery<ErrorOr<GetByIdBrandResponse>>;

    /// <summary>
    /// Get brand query handler.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="GetBrandByIdQueryHandler"/> class.
    /// </remarks>
    /// <param name="brandCacheService">The brand cache service.</param>
    internal sealed class GetBrandByIdQueryHandler(IGenericCacheService<BrandReadModel, Guid> brandCacheService) : IQueryHandler<GetBrandByIdQuery, ErrorOr<GetByIdBrandResponse>>
    {
        /// <summary>
        /// The brand cache service.
        /// </summary>
        private readonly IGenericCacheService<BrandReadModel, Guid> brandCacheService = brandCacheService;

        /// <summary>
        /// Handle and return a task of type erroror.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns><![CDATA[Task<ErrorOr<GetByIdBrandResponse>>]]></returns>
        public async ValueTask<ErrorOr<GetByIdBrandResponse>> Handle(GetBrandByIdQuery request, CancellationToken cancellationToken)
        {
            BrandReadModel? brand = await this.brandCacheService.GetOrSetByIdAsync(request.Id, cancellationToken: cancellationToken).ConfigureAwait(false);

            return brand == null ? (ErrorOr<GetByIdBrandResponse>)BrandErrors.NotFound : (ErrorOr<GetByIdBrandResponse>)BrandMapper.BrandReadModelToGetByIdBrandResponse(brand);
        }
    }
}
