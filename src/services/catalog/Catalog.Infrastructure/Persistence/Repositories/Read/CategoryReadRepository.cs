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
    public async Task<bool> ExistsByIdAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids == null)
            return false;

        var idList = ids as IList<Guid> ?? ids.ToList();
        if (idList.Count == 0)
            return false;

        var distinctCount = idList.Distinct().Count();

        var existingCount = await DbContext.Categories
            .AsNoTracking()
            .Where(category => idList.Contains(category.Id))
            .Select(category => category.Id)
            .Distinct()
            .CountAsync(cancellationToken);

        return existingCount == distinctCount;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CategoryReadModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await GetAllAsync(enableTracking: false, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CategoryReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await FindByIdAsync(id, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CategoryReadModel>> GetByParentIdAsync(Guid parentId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Categories
            .AsNoTracking()
            .Where(category => category.ParentId == parentId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PagedList<CategoryReadModel>> GetPagedCategoriesAsync(int page, int size, string? keyword, CancellationToken cancellationToken = default)
    {
        var query = DbContext.Categories.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(category => category.Name.Contains(keyword) ||
                                            (category.Description != null && category.Description.Contains(keyword)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(category => category.Name)
                             .Skip((page - 1) * size)
                             .Take(size)
                             .ToListAsync(cancellationToken);

        return new PagedList<CategoryReadModel>(items, totalCount, page, size);
    }
}
