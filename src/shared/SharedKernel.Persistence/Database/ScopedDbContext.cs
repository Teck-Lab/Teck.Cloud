using System.Linq.Expressions;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Domain;
using SharedKernel.Core.Events;
using SharedKernel.Core.MultiTenancy;

namespace SharedKernel.Persistence.Database;

/// <summary>Common model behavior for distinct read/write contexts and mixed tenant/global models.</summary>
public abstract class ScopedDbContext : DbContext, IMultiTenantDbContext
{
    private readonly OperationContext _operation;
    private readonly TimeProvider _clock;
    private readonly HashSet<object> _hardDeletes = new(ReferenceEqualityComparer.Instance);

    /// <summary>Initializes a new instance of the <see cref="ScopedDbContext"/> class.</summary>
    /// <param name="options">Provider and model options.</param>
    /// <param name="operation">The established operation context.</param>
    /// <param name="readOnly">Whether this context rejects all saves.</param>
    /// <param name="clock">Optional injected clock for audit and deletion timestamps.</param>
    protected ScopedDbContext(DbContextOptions options, OperationContext operation, bool readOnly, TimeProvider? clock = null)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(operation);
        _operation = operation;
        _clock = clock ?? TimeProvider.System;
        ReadOnly = readOnly;
    }

    /// <summary>Gets the captured data scope.</summary>
    public DataScope Scope => _operation.Scope;

    /// <summary>Gets a value indicating whether saves are prohibited.</summary>
    public bool ReadOnly { get; }

    /// <inheritdoc/>
    public ITenantInfo? TenantInfo => Scope.TenantId is { } id ? new TenantInfo { Id = id, Identifier = id } : null;

    /// <inheritdoc/>
    public TenantMismatchMode TenantMismatchMode => TenantMismatchMode.Throw;

    /// <inheritdoc/>
    public TenantNotSetMode TenantNotSetMode => TenantNotSetMode.Throw;

    /// <summary>Checks access before repository operations.</summary>
    /// <typeparam name="TEntity">The queried entity.</typeparam>
    /// <exception cref="InvalidOperationException">Tenant-owned data is accessed without tenant scope.</exception>
    public void EnsureAccess<TEntity>()
    {
        _ = Scope;
        if (typeof(ITenantScoped).IsAssignableFrom(typeof(TEntity)) && Scope.TenantId is null)
        {
            throw new InvalidOperationException("Tenant-owned data requires tenant scope.");
        }
    }

    /// <summary>Marks an explicit hard deletion; tenant checks still apply.</summary>
    /// <param name="entity">The tracked entity to delete permanently.</param>
    public void MarkHardDelete(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _hardDeletes.Add(entity);
        _ = Remove(entity);
    }

    /// <summary>Determines whether a deletion is explicitly permanent.</summary>
    /// <param name="entity">The tracked entity.</param>
    /// <returns>Whether soft deletion should be bypassed.</returns>
    public bool IsHardDelete(object entity) => _hardDeletes.Contains(entity);

    /// <inheritdoc/>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceWrite();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc/>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceWrite();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <inheritdoc/>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        _ = optionsBuilder.AddInterceptors(new RequiredScopeCommandInterceptor(), new AuditAndSoftDeleteInterceptor(_operation, _clock));
        if (ReadOnly)
        {
            _ = optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (var type in modelBuilder.Model.GetEntityTypes().Select(entityType => entityType.ClrType).ToArray())
        {
            var entity = modelBuilder.Entity(type);
            if (typeof(IHasDomainEvents).IsAssignableFrom(type))
            {
                _ = entity.Ignore(nameof(IHasDomainEvents.DomainEvents));
            }

            if (typeof(ITenantScoped).IsAssignableFrom(type))
            {
                _ = entity.IsMultiTenant();
                _ = entity.Property(nameof(ITenantScoped.TenantId)).IsConcurrencyToken();
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(type))
            {
                var parameter = Expression.Parameter(type, "entity");
                var filter = Expression.Lambda(Expression.Not(Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted))), parameter);
                _ = entity.HasQueryFilter("SoftDelete", filter);
            }
        }
    }

    private void EnforceWrite()
    {
        _ = Scope;
        if (ReadOnly)
        {
            throw new InvalidOperationException("The read context cannot save changes.");
        }

        if (ChangeTracker.Entries<ITenantScoped>().Any(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (Scope.TenantId is null)
            {
                throw new InvalidOperationException("Tenant writes require a tenant scope.");
            }

            this.EnforceMultiTenant();
        }
    }
}
