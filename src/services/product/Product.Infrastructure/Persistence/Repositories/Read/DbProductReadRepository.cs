// <copyright file="DbProductReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Product.Application.Product.Abstractions;
using Product.Application.Product.Features.GetProducts.V1;
using SharedKernel.Core.Pagination;

namespace Product.Infrastructure.Persistence.Repositories.Read;

/// <summary>
/// EF Core read repository for product queries.
/// </summary>
internal sealed class DbProductReadRepository(ProductReadDbContext dbContext) : IProductReadRepository
{
    /// <inheritdoc/>
    public async Task<PagedList<GetProductItemResponse>> GetPagedAsync(
        int page,
        int size,
        string? sortBy,
        bool sortDescending,
        CancellationToken cancellationToken)
    {
        IQueryable<Domain.Entities.ProductAggregate.Product> query = dbContext.Products.AsNoTracking();

        query = (sortBy?.ToLowerInvariant(), sortDescending) switch
        {
            ("sku", false) => query.OrderBy(product => product.SKU),
            ("sku", true) => query.OrderByDescending(product => product.SKU),
            ("name", true) => query.OrderByDescending(product => product.Name),
            _ => query.OrderBy(product => product.Name),
        };

        int totalItems = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        List<GetProductItemResponse> items = await query
            .Skip((page - 1) * size)
            .Take(size)
            .Select(product => new GetProductItemResponse(product.Id, product.Name, product.SKU, product.Barcode ?? string.Empty))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedList<GetProductItemResponse>(items, totalItems, page, size);
    }

    /// <inheritdoc/>
    public Task<bool> ExistsBySkuAsync(string sku, CancellationToken cancellationToken)
    {
        return dbContext.Products.AnyAsync(
            product => product.SKU == sku,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<GetProductItemResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Domain.Entities.ProductAggregate.Product? product = await dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(product => product.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (product is null)
        {
            return null;
        }

        return new GetProductItemResponse(product.Id, product.Name, product.SKU, product.Barcode ?? string.Empty);
    }
}
