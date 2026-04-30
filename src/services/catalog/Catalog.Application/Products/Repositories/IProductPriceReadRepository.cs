// <copyright file="IProductPriceReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Products.ReadModels;
using SharedKernel.Core.Database;

namespace Catalog.Application.Products.Repositories;

/// <summary>
/// Interface for ProductPrice read operations.
/// </summary>
public interface IProductPriceReadRepository : IGenericReadRepository<ProductPriceReadModel, Guid>
{
    /// <summary>
    /// Gets product prices by product IDs.
    /// </summary>
    /// <param name="productIds">The product identifiers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of product price read models.</returns>
    Task<IReadOnlyList<ProductPriceReadModel>> GetByProductIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default);
}
