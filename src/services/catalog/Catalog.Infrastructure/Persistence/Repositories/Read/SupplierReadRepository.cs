using Catalog.Application.Suppliers.ReadModels;
using Catalog.Application.Suppliers.Repositories;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Pagination;
using SharedKernel.Persistence.Database.EFCore;

namespace Catalog.Infrastructure.Persistence.Repositories.Read;

/// <summary>
/// Repository for read operations on Supplier entities.
/// </summary>
public sealed class SupplierReadRepository : GenericReadRepository<SupplierReadModel, Guid, ApplicationReadDbContext>, ISupplierReadRepository
{
    private readonly DbSet<SupplierReadModel> _suppliers;

    /// <summary>
    /// Initializes a new instance of the <see cref="SupplierReadRepository"/> class.
    /// </summary>
    /// <param name="readDbContext">The read database context.</param>
    public SupplierReadRepository(ApplicationReadDbContext readDbContext)
        : base(readDbContext)
    {
        _suppliers = DbContext.Suppliers;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SupplierReadModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await GetAllAsync(enableTracking: false, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SupplierReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await FindByIdAsync(id, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PagedList<SupplierReadModel>> GetPagedSuppliersAsync(int page, int size, string? keyword, CancellationToken cancellationToken = default)
    {
        var query = _suppliers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(supplier => supplier.Name.Contains(keyword) ||
                                   (supplier.Description != null && supplier.Description.Contains(keyword)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(supplier => supplier.Name)
                             .Skip((page - 1) * size)
                             .Take(size)
                             .ToListAsync(cancellationToken);

        return new PagedList<SupplierReadModel>(items, totalCount, page, size);
    }
}
