using Catalog.Application.Products.Mappings;
using Catalog.Application.Products.ReadModels;
using Catalog.Application.Products.Repositories;
using Catalog.Application.Products.Responses;
using Catalog.Domain.Entities.ProductAggregate.Errors;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Catalog.Application.Products.Features.GetProductById.V1
{
    /// <summary>
    /// Get Brand query.
    /// </summary>
    public sealed record GetProductByIdQuery(Guid Id) : IQuery<ErrorOr<ProductResponse>>;

    /// <summary>
    /// Get brand query handler.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="GetProductByIdQueryHandler"/> class.
    /// </remarks>
    /// <param name="cache">The cache.</param>
    internal sealed class GetProductByIdQueryHandler(IProductCache cache) : IQueryHandler<GetProductByIdQuery, ErrorOr<ProductResponse>>
    {
        /// <summary>
        /// The cache.
        /// </summary>
        private readonly IProductCache _cache = cache;

        /// <summary>
        /// Handle and return a task of type erroror.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns><![CDATA[Task<ErrorOr<BrandResponse>>]]></returns>
        public async ValueTask<ErrorOr<ProductResponse>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
        {
            ProductReadModel? product = await _cache.GetOrSetByIdAsync(request.Id, cancellationToken: cancellationToken);

            return product == null ? (ErrorOr<ProductResponse>)ProductErrors.NotFound : (ErrorOr<ProductResponse>)ProductMappings.ProductReadModelToProductResponse(product);
        }
    }
}
