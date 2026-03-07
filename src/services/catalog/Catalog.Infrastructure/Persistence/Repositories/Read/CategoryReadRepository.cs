// <copyright file="CategoryReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Application.Categories.ReadModels;
using Catalog.Application.Categories.Repositories;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Pagination;
using SharedKernel.Persistence.Database.EFCore;

namespace Catalog.Infrastructure.Persistence.Repositories.Read;

/// <summary>
/// Repository for read operations on Category entities.
/// </summary>
public sealed class CategoryReadRepository : GenericReadRepository<CategoryReadModel, Guid, ApplicationReadDbContext>, ICategoryReadRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CategoryReadRepository"/> class.
    /// </summary>
    /// <param name="readDbContext">The read database context.</param>
    public CategoryReadRepository(ApplicationReadDbContext readDbContext)
        : base(readDbContext)
    {
    }

    /// <summary>
    /// Checks if all specified category IDs exist in the database.
    /// </summary>
    /// <param name="ids">The collection of category IDs to check.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>True if all IDs exist; otherwise, false.</returns>
    public async Task<bool> ExistsByIdAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids == null)
        {
            return false;
        }

        var idList = ids as IList<Guid> ?? ids.ToList();
        if (idList.Count == 0)
        {
            return false;
        }

        var distinctCount = idList.Distinct().Count();

        var existingCount = await this.DbContext.Categories
            .AsNoTracking()
            .Where(category => idList.Contains(category.Id))
            .Select(category => category.Id)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        return existingCount == distinctCount;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CategoryReadModel>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await this.GetAllAsync(false, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<CategoryReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await this.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CategoryReadModel>> GetByParentIdAsync(Guid parentId, CancellationToken cancellationToken)
    {
        return await this.DbContext.Categories
            .AsNoTracking()
            .Where(category => category.ParentId == parentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PagedList<CategoryReadModel>> GetPagedCategoriesAsync(int page, int size, string? keyword, CancellationToken cancellationToken)
    {
        var query = this.DbContext.Categories.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(category => category.Name.Contains(keyword) ||
                                            (category.Description != null && category.Description.Contains(keyword)));
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderBy(category => category.Name)
                             .Skip((page - 1) * size)
                             .Take(size)
                     .ToListAsync(cancellationToken)
                     .ConfigureAwait(false);

        return new PagedList<CategoryReadModel>(items, totalCount, page, size);
    }
}
