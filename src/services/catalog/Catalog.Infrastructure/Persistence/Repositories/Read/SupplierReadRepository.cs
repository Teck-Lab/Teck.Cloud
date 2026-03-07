// <copyright file="SupplierReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

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
    private readonly DbSet<SupplierReadModel> suppliers;

    /// <summary>
    /// Initializes a new instance of the <see cref="SupplierReadRepository"/> class.
    /// </summary>
    /// <param name="readDbContext">The read database context.</param>
    public SupplierReadRepository(ApplicationReadDbContext readDbContext)
        : base(readDbContext)
    {
        this.suppliers = this.DbContext.Suppliers;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SupplierReadModel>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await this.GetAllAsync(false, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<SupplierReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await this.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PagedList<SupplierReadModel>> GetPagedSuppliersAsync(int page, int size, string? keyword, CancellationToken cancellationToken)
    {
        var query = this.suppliers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(supplier => supplier.Name.Contains(keyword) ||
                                   (supplier.Description != null && supplier.Description.Contains(keyword)));
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderBy(supplier => supplier.Name)
                             .Skip((page - 1) * size)
                             .Take(size)
                     .ToListAsync(cancellationToken)
                     .ConfigureAwait(false);

        return new PagedList<SupplierReadModel>(items, totalCount, page, size);
    }
}
