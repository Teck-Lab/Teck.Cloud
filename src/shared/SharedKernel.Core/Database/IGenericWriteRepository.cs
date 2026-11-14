using System.Linq.Expressions;
using Ardalis.Specification;
using SharedKernel.Core.Domain;

namespace SharedKernel.Core.Database;

/// <summary>
/// Write repository for aggregate roots; child entities are mutated through their root.
/// </summary>
/// <typeparam name="TEntity">The type of the entity managed by this repository.</typeparam>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
public interface IGenericWriteRepository<TEntity, in TId> : IGenericReadRepository<TEntity, TId>
    where TEntity : class, IAggregateRoot
{
    /// <summary>Loads an accessible entity tracked by the scoped write context for mutation.</summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tracked entity, or null when unavailable in the current data scope.</returns>
    Task<TEntity?> LoadForUpdateAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>Loads one tracked aggregate with its specified relationships; duplicate matches are rejected.</summary>
    /// <param name="specification">An unpaged single-result query without filter bypass, no-tracking or post-processing.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tracked aggregate, or null when absent in the current scope.</returns>
    Task<TEntity?> LoadForUpdateAsync(ISingleResultSpecification<TEntity> specification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add entity.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update entity.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    void Update(TEntity entity);

    /// <summary>
    /// Delete list of entities.
    /// </summary>
    /// <param name="entities">The entities to delete.</param>
    void DeleteRange(IReadOnlyList<TEntity> entities);

    /// <summary>
    /// Delete a specific entity.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    void Delete(TEntity entity);

    /// <summary>
    /// Stages a bounded, tenant-filtered batch for soft deletion; the transaction owner saves it.
    /// </summary>
    /// <param name="ids">The identifiers of the entities to soft delete.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ExecuteSoftDeleteAsync(IReadOnlyCollection<TId> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages matching entities for soft deletion while preserving tenant, audit and transaction behavior.
    /// </summary>
    /// <param name="predicate">The predicate selecting the entities to soft delete.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ExecuteSoftDeleteByAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a bounded hard-delete batch, retaining tenant restrictions. The transaction owner saves it.
    /// </summary>
    /// <param name="ids">The identifiers of the entities to hard delete.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    Task ExecuteHardDeleteAsync(IReadOnlyCollection<TId> ids, CancellationToken cancellationToken = default);
}
