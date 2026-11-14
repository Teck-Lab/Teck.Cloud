using System.Linq.Expressions;
using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Database;
using SharedKernel.Core.Domain;

namespace SharedKernel.Persistence.Database;

/// <summary>Tenant-aware, untracked read access with bounded materialization.</summary>
/// <typeparam name="TEntity">The entity or projection.</typeparam>
/// <typeparam name="TId">The identifier.</typeparam>
/// <typeparam name="TContext">The service read context.</typeparam>
/// <param name="context">The scoped context.</param>
public class GenericReadRepository<TEntity, TId, TContext>(TContext context) : IGenericReadRepository<TEntity, TId>
    where TEntity : class, IReadModel<TId>
    where TContext : ScopedDbContext
{
    /// <summary>Gets the scoped context.</summary>
    protected TContext Context { get; } = context;

    /// <summary>Gets the maximum result size for list operations.</summary>
    protected virtual int MaximumResults => 1000;

    /// <inheritdoc/>
    public Task<TEntity?> FindByIdAsync(TId id, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(entity => Equals(entity.Id, id), cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<TEntity>> FindByIdsAsync(IEnumerable<TId> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var values = ids.ToArray();
        return BoundedAsync(Query().Where(entity => values.Contains(entity.Id)), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TEntity?> FindOneAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(predicate, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        BoundedAsync(Query().Where(predicate), cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default) =>
        BoundedAsync(Query(), cancellationToken);

    /// <inheritdoc/>
    public Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        Query().AnyAsync(predicate, cancellationToken);

    /// <inheritdoc/>
    public Task<TEntity?> FirstOrDefaultAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default) =>
        Apply(specification).FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<TResult?> FirstOrDefaultAsync<TResult>(ISpecification<TEntity, TResult> specification, CancellationToken cancellationToken = default) =>
        Project(specification).FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<TEntity?> SingleOrDefaultAsync(ISingleResultSpecification<TEntity> specification, CancellationToken cancellationToken = default) =>
        Apply(specification, singleResult: true).SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<TResult?> SingleOrDefaultAsync<TResult>(ISingleResultSpecification<TEntity, TResult> specification, CancellationToken cancellationToken = default) =>
        Project(specification, singleResult: true).SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<TEntity>> ListAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default) =>
        BoundedAsync(Apply(specification), cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<TResult>> ListAsync<TResult>(ISpecification<TEntity, TResult> specification, CancellationToken cancellationToken = default) =>
        BoundedAsync(Project(specification), cancellationToken);

    /// <inheritdoc/>
    public Task<int> CountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default) =>
        Apply(specification, criteriaOnly: true).CountAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<bool> AnyAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default) =>
        Apply(specification, criteriaOnly: true).AnyAsync(cancellationToken);

    /// <summary>Enforces repository scope, tracking ownership and single-result semantics.</summary>
    /// <param name="specification">The query to validate.</param>
    /// <param name="forWrite">Whether the query must load tracked aggregates.</param>
    /// <param name="singleResult">Whether pagination would hide a duplicate match.</param>
    /// <exception cref="InvalidOperationException">The specification conflicts with repository guarantees.</exception>
    /// <exception cref="NotSupportedException">Client-side post-processing is requested.</exception>
    protected static void ValidateSpecification(ISpecification<TEntity> specification, bool forWrite = false, bool singleResult = false)
    {
        ArgumentNullException.ThrowIfNull(specification);
        if (specification.IgnoreQueryFilters)
        {
            throw new InvalidOperationException("Specifications cannot bypass query filters.");
        }

        if (forWrite ? specification.AsNoTracking || specification.AsNoTrackingWithIdentityResolution : specification.AsTracking)
        {
            throw new InvalidOperationException("Specification tracking conflicts with the repository operation.");
        }

        if (specification.PostProcessingAction is not null)
        {
            throw new NotSupportedException("Use SQL projections or explicitly transform materialized results outside the specification.");
        }

        if (singleResult && (specification.Skip >= 0 || specification.Take >= 0))
        {
            throw new InvalidOperationException("Single-result specifications cannot use Skip or Take; pagination can hide duplicate matches.");
        }
    }

    /// <summary>Creates a checked untracked query.</summary>
    /// <returns>The query.</returns>
    protected IQueryable<TEntity> Query()
    {
        Context.EnsureAccess<TEntity>();
        return Context.Set<TEntity>().AsNoTracking();
    }

    private IQueryable<TResult> Project<TResult>(ISpecification<TEntity, TResult> specification, bool singleResult = false)
    {
        ValidateSpecification(specification, singleResult: singleResult);
        return specification.PostProcessingAction is not null
            ? throw new NotSupportedException("Use SQL projections or explicitly transform materialized results outside the specification.")
            : SpecificationEvaluator.Default.GetQuery(Query(), specification);
    }

    private IQueryable<TEntity> Apply(ISpecification<TEntity> specification, bool criteriaOnly = false, bool singleResult = false)
    {
        ValidateSpecification(specification, singleResult: singleResult);
        return SpecificationEvaluator.Default.GetQuery(Query(), specification, criteriaOnly);
    }

    private async Task<IReadOnlyList<TResult>> BoundedAsync<TResult>(IQueryable<TResult> query, CancellationToken cancellationToken)
    {
        var rows = await query.Take(MaximumResults + 1).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Count > MaximumResults
            ? throw new InvalidOperationException("Query exceeds the result limit; use a paged specification.")
            : rows;
    }
}
