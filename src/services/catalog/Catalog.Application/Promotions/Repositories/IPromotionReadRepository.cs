using Catalog.Application.Promotions.ReadModels;
using SharedKernel.Core.Database;
using SharedKernel.Core.Pagination;

namespace Catalog.Application.Promotions.Repositories;

/// <summary>
/// Repository interface for read operations on Promotion entities.
/// </summary>
public interface IPromotionReadRepository : IGenericReadRepository<PromotionReadModel, Guid>
{
    /// <summary>
    /// Gets all promotions.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of promotion read models.</returns>
    Task<IReadOnlyList<PromotionReadModel>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a promotion by ID.
    /// </summary>
    /// <param name="id">The promotion ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The promotion read model if found, otherwise null.</returns>
    Task<PromotionReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active promotions.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of active promotion read models.</returns>
    Task<IReadOnlyList<PromotionReadModel>> GetActivePromotionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets promotions by category ID.
    /// </summary>
    /// <param name="categoryId">The category ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of promotion read models.</returns>
    Task<IReadOnlyList<PromotionReadModel>> GetByCategoryIdAsync(Guid categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get paged promotions.
    /// </summary>
    /// <param name="page">The page number.</param>
    /// <param name="size">The page size.</param>
    /// <param name="keyword">The search keyword.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A paged list of promotion read models.</returns>
    Task<PagedList<PromotionReadModel>> GetPagedPromotionsAsync(int page, int size, string? keyword, CancellationToken cancellationToken = default);
}
