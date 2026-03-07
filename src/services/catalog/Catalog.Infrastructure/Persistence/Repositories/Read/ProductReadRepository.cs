// <copyright file="ProductReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Products.ReadModels;
using Catalog.Application.Products.Repositories;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Pagination;
using SharedKernel.Persistence.Database.EFCore;

namespace Catalog.Infrastructure.Persistence.Repositories.Read;

/// <summary>
/// Repository for read operations on Product entities.
/// </summary>
public sealed class ProductReadRepository : GenericReadRepository<ProductReadModel, Guid, ApplicationReadDbContext>, IProductReadRepository
{
    private readonly DbSet<ProductReadModel> products;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductReadRepository"/> class.
    /// </summary>
    /// <param name="readDbContext">The read database context.</param>
    public ProductReadRepository(ApplicationReadDbContext readDbContext)
        : base(readDbContext)
    {
        this.products = this.DbContext.Products;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProductReadModel>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await this.GetAllAsync(false, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ProductReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await this.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProductReadModel>> GetByBrandIdAsync(Guid brandId, CancellationToken cancellationToken)
    {
        return await this.products
            .AsNoTracking()
            .Where(product => product.BrandId == brandId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProductReadModel>> GetByCategoryIdAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        return await this.products
            .AsNoTracking()
            .Where(product => product.CategoryId == categoryId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PagedList<ProductReadModel>> GetPagedProductsAsync(int page, int size, string? keyword, CancellationToken cancellationToken)
    {
        var query = this.products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(product =>
                (product.Name != null && product.Name.Contains(keyword)) ||
                (product.Description != null && product.Description.Contains(keyword)));
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderBy(product => product.Name)
                              .Skip((page - 1) * size)
                              .Take(size)
                      .ToListAsync(cancellationToken)
                      .ConfigureAwait(false);

        return new PagedList<ProductReadModel>(items, totalCount, page, size);
    }

    /// <inheritdoc/>
    public async Task<ProductReadModel?> GetBySkuAsync(string sku, CancellationToken cancellationToken)
    {
        return await this.products
            .AsNoTracking()
            .FirstOrDefaultAsync(product => product.Sku == sku, cancellationToken)
            .ConfigureAwait(false);
    }
}
