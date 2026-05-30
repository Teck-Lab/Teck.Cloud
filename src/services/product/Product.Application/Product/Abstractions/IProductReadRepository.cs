// <copyright file="IProductReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Product.Application.Product.Features.GetProducts.V1;
using SharedKernel.Core.Pagination;

namespace Product.Application.Product.Abstractions;

/// <summary>
/// Read repository for product queries.
/// </summary>
public interface IProductReadRepository
{
    /// <summary>
    /// Gets a paginated list of products.
    /// </summary>
    Task<PagedList<GetProductItemResponse>> GetPagedAsync(
        int page,
        int size,
        string? sortBy,
        bool sortDescending,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks whether a product with the given SKU already exists.
    /// </summary>
    Task<bool> ExistsBySkuAsync(string sku, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a product response by its identifier.
    /// </summary>
    Task<GetProductItemResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
