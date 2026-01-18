using SharedKernel.Core.Database;

namespace Catalog.Domain.Entities.CategoryAggregate.Repositories;

/// <summary>
/// Repository interface for write operations on Category entities.
/// </summary>
public interface ICategoryWriteRepository : IGenericWriteRepository<Category, Guid>
{
    /// <summary>
    /// Gets a category by name.
    /// </summary>
    /// <param name="name">The name of the category.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The category if found, otherwise null.</returns>
    Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets categories by parent ID.
    /// </summary>
    /// <param name="parentId">The parent category ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of categories.</returns>
    Task<IReadOnlyList<Category>> GetByParentIdAsync(Guid parentId, CancellationToken cancellationToken = default);
}
