using Catalog.Application.ProductPriceTypes.ReadModels;
using Catalog.Application.ProductPriceTypes.Repositories;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Pagination;
using SharedKernel.Persistence.Database.EFCore;

namespace Catalog.Infrastructure.Persistence.Repositories.Read;

/// <summary>
/// Repository for read operations on ProductPriceType entities.
/// </summary>
public sealed class ProductPriceTypeReadRepository : GenericReadRepository<ProductPriceTypeReadModel, Guid, ApplicationReadDbContext>, IProductPriceTypeReadRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProductPriceTypeReadRepository"/> class.
    /// </summary>
    /// <param name="readDbContext">The read database context.</param>
    public ProductPriceTypeReadRepository(ApplicationReadDbContext readDbContext)
        : base(readDbContext)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProductPriceTypeReadModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await GetAllAsync(enableTracking: false, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ProductPriceTypeReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await FindByIdAsync(id, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PagedList<ProductPriceTypeReadModel>> GetPagedProductPriceTypesAsync(int page, int size, string? keyword, CancellationToken cancellationToken = default)
    {
        var query = DbContext.ProductPriceTypes.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(priceType => priceType.Name.Contains(keyword) ||
                                   (priceType.Description != null && priceType.Description.Contains(keyword)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(priceType => priceType.Name)
                             .Skip((page - 1) * size)
                             .Take(size)
                             .ToListAsync(cancellationToken);

        return new PagedList<ProductPriceTypeReadModel>(items, totalCount, page, size);
    }
}
