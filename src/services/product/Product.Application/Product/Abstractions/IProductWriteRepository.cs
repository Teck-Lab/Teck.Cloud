// <copyright file="IProductWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Product.Application.Product.Abstractions;

/// <summary>
/// Write repository for <see cref="Domain.Entities.ProductAggregate.Product"/> aggregate.
/// </summary>
public interface IProductWriteRepository
{
    /// <summary>
    /// Adds a new product.
    /// </summary>
    Task AddAsync(Domain.Entities.ProductAggregate.Product product, CancellationToken cancellationToken);

    /// <summary>
    /// Checks whether a product with the given SKU already exists.
    /// </summary>
    Task<bool> ExistsBySkuAsync(string sku, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a product by its identifier.
    /// </summary>
    Task<Domain.Entities.ProductAggregate.Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
