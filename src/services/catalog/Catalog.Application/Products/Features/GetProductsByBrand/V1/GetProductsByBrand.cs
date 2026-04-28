// <copyright file="GetProductsByBrand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Products.Features.GetPaginatedProducts.V1;
using Catalog.Application.Products.ReadModels;
using Catalog.Application.Products.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;

namespace Catalog.Application.Products.Features.GetProductsByBrand.V1;

/// <summary>
/// Query for retrieving products by brand.
/// </summary>
/// <param name="BrandId">Brand identifier.</param>
public sealed record GetProductsByBrandQuery(Guid BrandId)
    : IQuery<ErrorOr<List<GetPaginatedProductsResponse>>>;

/// <summary>
/// Handler for products-by-brand queries.
/// </summary>
internal sealed class GetProductsByBrandQueryHandler(IProductReadRepository productReadRepository)
    : IQueryHandler<GetProductsByBrandQuery, ErrorOr<List<GetPaginatedProductsResponse>>>
{
    private readonly IProductReadRepository productReadRepository = productReadRepository;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<List<GetPaginatedProductsResponse>>> Handle(
        GetProductsByBrandQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ProductReadModel> products = await this.productReadRepository
            .GetByBrandIdAsync(request.BrandId, cancellationToken)
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
