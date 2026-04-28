// <copyright file="GetProductsByCategory.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Products.Features.GetPaginatedProducts.V1;
using Catalog.Application.Products.ReadModels;
using Catalog.Application.Products.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Catalog.Application.Products.Features.GetProductsByCategory.V1;

/// <summary>
/// Query for retrieving products by category.
/// </summary>
/// <param name="CategoryId">Category identifier.</param>
public sealed record GetProductsByCategoryQuery(Guid CategoryId)
    : IQuery<ErrorOr<List<GetPaginatedProductsResponse>>>;

/// <summary>
/// Handler for products-by-category queries.
/// </summary>
internal sealed class GetProductsByCategoryQueryHandler(IProductReadRepository productReadRepository)
    : IQueryHandler<GetProductsByCategoryQuery, ErrorOr<List<GetPaginatedProductsResponse>>>
{
    private readonly IProductReadRepository productReadRepository = productReadRepository;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<List<GetPaginatedProductsResponse>>> Handle(
        GetProductsByCategoryQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ProductReadModel> products = await this.productReadRepository
            .GetByCategoryIdAsync(request.CategoryId, cancellationToken)
            .ConfigureAwait(false);

        List<GetPaginatedProductsResponse> response = products
            .Select(product => new GetPaginatedProductsResponse
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Sku = product.Sku,
                BrandId = product.BrandId,
                BrandName = product.BrandName,
                CategoryId = product.CategoryId,
                CategoryName = product.CategoryName,
                SupplierId = product.SupplierId,
                SupplierName = product.SupplierName,
                ImageUrl = product.ImageUrl,
            })
            .ToList();

        return response;
    }
}
