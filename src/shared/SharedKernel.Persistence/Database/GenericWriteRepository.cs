using System.Linq.Expressions;
using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Database;
using SharedKernel.Core.Domain;

namespace SharedKernel.Persistence.Database;

/// <summary>Write-side operations using the same scoped context as the unit of work.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TId">The identifier type.</typeparam>
/// <typeparam name="TContext">The service write context.</typeparam>
/// <param name="context">The scoped write context.</param>
public class GenericWriteRepository<TEntity, TId, TContext>(TContext context)
    : GenericReadRepository<TEntity, TId, TContext>(context), IGenericWriteRepository<TEntity, TId>
    where TEntity : class, IReadModel<TId>, IAggregateRoot
    where TContext : ScopedDbContext
{
    /// <inheritdoc/>
    public Task<TEntity?> LoadForUpdateAsync(TId id, CancellationToken cancellationToken = default)
    {
        EnsureWrite();
        return Context.Set<TEntity>().AsTracking().FirstOrDefaultAsync(entity => Equals(entity.Id, id), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TEntity?> LoadForUpdateAsync(ISingleResultSpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        EnsureWrite();
        ValidateSpecification(specification, forWrite: true, singleResult: true);
        return SpecificationEvaluator.Default.GetQuery(Context.Set<TEntity>(), specification)
            .AsTracking().SingleOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        EnsureWrite();
        _ = await Context.Set<TEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Update(TEntity entity)
    {
        EnsureWrite();
        _ = Context.Set<TEntity>().Update(entity);
    }

    /// <inheritdoc/>
    public void DeleteRange(IReadOnlyList<TEntity> entities)
    {
        EnsureWrite();
        Context.RemoveRange(entities);
    }

    /// <inheritdoc/>
    public void Delete(TEntity entity)
    {
        EnsureWrite();
        _ = Context.Remove(entity);
    }

    /// <inheritdoc/>
    public Task ExecuteSoftDeleteAsync(IReadOnlyCollection<TId> ids, CancellationToken cancellationToken = default) =>
        ExecuteSoftDeleteByAsync(entity => ids.Contains(entity.Id), cancellationToken);

    /// <inheritdoc/>
    public async Task ExecuteSoftDeleteByAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        EnsureWrite();
        if (!typeof(ISoftDeletable).IsAssignableFrom(typeof(TEntity)))
        {
            throw new InvalidOperationException("The entity does not support soft deletion.");
        }

        var rows = await LoadBoundedAsync(Context.Set<TEntity>().Where(predicate), cancellationToken).ConfigureAwait(false);
        Context.RemoveRange(rows);
    }

    /// <inheritdoc/>
    public async Task ExecuteHardDeleteAsync(IReadOnlyCollection<TId> ids, CancellationToken cancellationToken = default)
    {
        EnsureWrite();
        var query = Context.Set<TEntity>().IgnoreQueryFilters(["SoftDelete"]).Where(entity => ids.Contains(entity.Id));
        foreach (var entity in await LoadBoundedAsync(query, cancellationToken).ConfigureAwait(false))
        {
            Context.MarkHardDelete(entity);
        }
    }

    private void EnsureWrite()
    {
        Context.EnsureAccess<TEntity>();
        if (Context.ReadOnly)
        {
            throw new InvalidOperationException("A write repository requires a write context.");
        }
    }

    private async Task<List<TEntity>> LoadBoundedAsync(IQueryable<TEntity> query, CancellationToken cancellationToken)
    {
        var rows = await query.Take(MaximumResults + 1).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Count > MaximumResults
            ? throw new InvalidOperationException("Mutation exceeds the batch limit.")
            : rows;
    }
}
