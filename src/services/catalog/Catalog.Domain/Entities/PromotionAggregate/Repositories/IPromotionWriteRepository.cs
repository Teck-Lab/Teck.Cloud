using SharedKernel.Core.Database;

namespace Catalog.Domain.Entities.PromotionAggregate.Repositories;

/// <summary>
/// Repository interface for write operations on Promotion entities.
/// </summary>
public interface IPromotionWriteRepository : IGenericWriteRepository<Promotion, Guid>
{
    /// <summary>
    /// Gets a promotion by name.
    /// </summary>
    /// <param name="name">The name of the promotion.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The promotion if found, otherwise null.</returns>
    Task<Promotion?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active promotions.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of active promotions.</returns>
    Task<IReadOnlyList<Promotion>> GetActivePromotionsAsync(CancellationToken cancellationToken = default);
}
