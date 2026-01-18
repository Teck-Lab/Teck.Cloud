using Catalog.Application.Brands.Features.Responses;
using Catalog.Application.Brands.Mappings;
using Catalog.Application.Brands.ReadModels;
using Catalog.Application.Brands.Repositories;
using Catalog.Domain.Entities.BrandAggregate.Errors;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Catalog.Application.Brands.Features.GetBrandById.V1
{
    /// <summary>
    /// Get Brand query.
    /// </summary>
    public sealed record GetBrandByIdQuery(Guid Id) : IQuery<ErrorOr<BrandResponse>>;

    /// <summary>
    /// Get brand query handler.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="GetBrandByIdQueryHandler"/> class.
    /// </remarks>
    /// <param name="cache">The cache.</param>
    internal sealed class GetBrandByIdQueryHandler(IBrandCache cache) : IQueryHandler<GetBrandByIdQuery, ErrorOr<BrandResponse>>
    {
        /// <summary>
        /// The cache.
        /// </summary>
        private readonly IBrandCache _cache = cache;

        /// <summary>
        /// Handle and return a task of type erroror.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns><![CDATA[Task<ErrorOr<BrandResponse>>]]></returns>
        public async ValueTask<ErrorOr<BrandResponse>> Handle(GetBrandByIdQuery request, CancellationToken cancellationToken)
        {
            BrandReadModel? brand = await _cache.GetOrSetByIdAsync(request.Id, cancellationToken: cancellationToken);

            return brand == null ? (ErrorOr<BrandResponse>)BrandErrors.NotFound : (ErrorOr<BrandResponse>)BrandMapper.BrandReadModelToBrandResponse(brand);
        }
    }
}
