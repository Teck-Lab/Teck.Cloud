using Catalog.Application.Categories.ReadModels;
using SharedKernel.Core.Database;
using SharedKernel.Core.Pagination;

namespace Catalog.Application.Categories.Repositories;

/// <summary>
/// Repository interface for read operations on Category entities.
/// </summary>
public interface ICategoryReadRepository : IGenericReadRepository<CategoryReadModel, Guid>
{
    /// <summary>
    /// Gets all categories.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of category read models.</returns>
    Task<IReadOnlyList<CategoryReadModel>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a category by ID.
    /// </summary>
    /// <param name="id">The category ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The category read model if found, otherwise null.</returns>
    Task<CategoryReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets categories by parent category ID.
    /// </summary>
    /// <param name="parentId">The parent category ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of category read models.</returns>
    Task<IReadOnlyList<CategoryReadModel>> GetByParentIdAsync(Guid parentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get paged categories.
    /// </summary>
    /// <param name="page">The page number.</param>
    /// <param name="size">The page size.</param>
    /// <param name="keyword">The search keyword.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A paged list of category read models.</returns>
    Task<PagedList<CategoryReadModel>> GetPagedCategoriesAsync(int page, int size, string? keyword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if categories exist by their IDs.
    /// </summary>
    /// <param name="ids">The collection of category IDs to check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if all specified categories exist; otherwise, false.</returns>
    Task<bool> ExistsByIdAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}
