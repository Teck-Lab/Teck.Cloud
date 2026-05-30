// <copyright file="GetProducts.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using ErrorOr;
using Product.Application.Product.Abstractions;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.Pagination;

namespace Product.Application.Product.Features.GetProducts.V1;

/// <summary>
/// Query for a paginated list of products.
/// </summary>
/// <param name="Page">Page number (1-based).</param>
/// <param name="Size">Page size.</param>
/// <param name="SortBy">Column to sort by: productId, name, sku. Defaults to productId.</param>
/// <param name="SortDescending">Sort direction; true for descending.</param>
public sealed record GetProductsQuery(int Page, int Size, string? SortBy, bool SortDescending)
    : IQuery<ErrorOr<PagedList<GetProductItemResponse>>>;

/// <summary>
/// Handler for <see cref="GetProductsQuery"/>.
/// </summary>
internal sealed class GetProductsQueryHandler(IProductReadRepository readRepository)
    : IQueryHandler<GetProductsQuery, ErrorOr<PagedList<GetProductItemResponse>>>
{
    private readonly IProductReadRepository readRepository = readRepository;

    /// <inheritdoc/>
    public async ValueTask<ErrorOr<PagedList<GetProductItemResponse>>> Handle(
        GetProductsQuery request,
        CancellationToken cancellationToken)
    {
        int page = request.Page < 1 ? 1 : request.Page;
        int size = request.Size < 1 ? 10 : request.Size;

        PagedList<GetProductItemResponse> result = await this.readRepository
            .GetPagedAsync(page, size, request.SortBy, request.SortDescending, cancellationToken)
            .ConfigureAwait(false);

        return result;
    }
}
