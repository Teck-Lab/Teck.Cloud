// <copyright file="DbProductWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Product.Application.Product.Abstractions;
using SharedKernel.Persistence.Database.EFCore;

namespace Product.Infrastructure.Persistence.Repositories.Write;

/// <summary>
/// EF Core write repository for <see cref="Domain.Entities.ProductAggregate.Product"/>.
/// </summary>
public sealed class DbProductWriteRepository(
    ProductWriteDbContext dbContext,
    IHttpContextAccessor httpContextAccessor)
    : GenericWriteRepository<Domain.Entities.ProductAggregate.Product, Guid, ProductWriteDbContext>(dbContext, httpContextAccessor),
      IProductWriteRepository
{
    /// <inheritdoc/>
    public new async Task AddAsync(Domain.Entities.ProductAggregate.Product product, CancellationToken cancellationToken)
    {
        await this.DbContext.Products.AddAsync(product, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<bool> ExistsBySkuAsync(string sku, CancellationToken cancellationToken)
    {
        return this.DbContext.Products.AnyAsync(
            product => product.SKU == sku,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Domain.Entities.ProductAggregate.Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return this.DbContext.Products.FindAsync([id], cancellationToken).AsTask();
    }
}
