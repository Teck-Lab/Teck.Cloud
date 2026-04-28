// <copyright file="GetPaginatedProducts.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Products.ReadModels;
using Catalog.Application.Products.Repositories;
using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Pagination;

namespace Catalog.Application.Products.Features.GetPaginatedProducts.V1;

/// <summary>
/// Query for paginated products.
/// </summary>
/// <param name="Page">Page number (1-based).</param>
/// <param name="Size">Page size.</param>
/// <param name="Keyword">Optional keyword filter.</param>
public sealed record GetPaginatedProductsQuery(int Page, int Size, string? Keyword)
    : IQuery<ErrorOr<PagedList<GetPaginatedProductsResponse>>>;

/// <summary>
/// Handler for paginated product queries.
/// </summary>
internal sealed class GetPaginatedProductsQueryHandler(IProductReadRepository productReadRepository)
    : IQueryHandler<GetPaginatedProductsQuery, ErrorOr<PagedList<GetPaginatedProductsResponse>>>
{
    private readonly IProductReadRepository productReadRepository = productReadRepository;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<PagedList<GetPaginatedProductsResponse>>> Handle(
        GetPaginatedProductsQuery request,
        CancellationToken cancellationToken)
    {
        PagedList<ProductReadModel> products = await this.productReadRepository
            .GetPagedProductsAsync(request.Page, request.Size, request.Keyword, cancellationToken)
            .ConfigureAwait(false);

        IList<GetPaginatedProductsResponse> items = products.Items
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

        return new PagedList<GetPaginatedProductsResponse>(items, products.TotalItems, products.Page, products.Size);
    }
}
