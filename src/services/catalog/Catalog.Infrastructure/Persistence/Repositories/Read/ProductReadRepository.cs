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
    private readonly DbSet<ProductReadModel> _products;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductReadRepository"/> class.
    /// </summary>
    /// <param name="readDbContext">The read database context.</param>
    public ProductReadRepository(ApplicationReadDbContext readDbContext)
        : base(readDbContext)
    {
        _products = DbContext.Products;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProductReadModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await GetAllAsync(enableTracking: false, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ProductReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await FindByIdAsync(id, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProductReadModel>> GetByBrandIdAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        return await _products
            .AsNoTracking()
            .Where(product => product.BrandId == brandId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProductReadModel>> GetByCategoryIdAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        return await _products
            .AsNoTracking()
            .Where(product => product.CategoryId == categoryId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PagedList<ProductReadModel>> GetPagedProductsAsync(int page, int size, string? keyword, CancellationToken cancellationToken = default)
    {
        var query = _products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(product =>
                (product.Name != null && product.Name.Contains(keyword)) ||
                (product.Description != null && product.Description.Contains(keyword)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(product => product.Name)
                              .Skip((page - 1) * size)
                              .Take(size)
                              .ToListAsync(cancellationToken);

        return new PagedList<ProductReadModel>(items, totalCount, page, size);
    }

    /// <inheritdoc/>
    public async Task<ProductReadModel?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        return await _products
            .AsNoTracking()
            .FirstOrDefaultAsync(product => product.Sku == sku, cancellationToken);
    }
}
