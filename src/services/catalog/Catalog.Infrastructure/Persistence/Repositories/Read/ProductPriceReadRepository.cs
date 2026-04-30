// <copyright file="ProductPriceReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Products.ReadModels;
using Catalog.Application.Products.Repositories;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence.Database.EFCore;

namespace Catalog.Infrastructure.Persistence.Repositories.Read;

/// <summary>
/// Repository for ProductPrice read operations.
/// </summary>
public sealed class ProductPriceReadRepository : GenericReadRepository<ProductPriceReadModel, Guid, ApplicationReadDbContext>, IProductPriceReadRepository
{
    private readonly DbSet<ProductPriceReadModel> productPrices;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductPriceReadRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public ProductPriceReadRepository(
        ApplicationReadDbContext dbContext)
        : base(dbContext)
    {
        this.productPrices = this.DbContext.ProductPrices;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProductPriceReadModel>> GetByProductIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
        {
            return [];
        }

        return await this.productPrices
            .AsNoTracking()
            .Where(productPrice => productIds.Contains(productPrice.ProductId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
