namespace SharedKernel.Core.Caching;

/// <summary>Read-only interface for tenant-scoped, read-through cached projections.</summary>
/// <typeparam name="TEntity">The cached read model.</typeparam>
/// <typeparam name="TId">The read model identifier.</typeparam>
public interface IReadCacheService<TEntity, in TId>
    where TEntity : class
{
    /// <summary>Reads a cached projection or loads it on a miss.</summary>
    /// <param name="id">The projection identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached or loaded projection.</returns>
    Task<TEntity?> GetOrSetByIdAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>Reads a cached projection without loading it on a miss.</summary>
    /// <param name="id">The projection identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached projection, if present.</returns>
    Task<TEntity?> TryGetByIdAsync(TId id, CancellationToken cancellationToken = default);
}
