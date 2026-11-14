using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SharedKernel.Core.Database;
using SharedKernel.Core.Events;

namespace SharedKernel.Persistence.Database;

/// <summary>Explicit EF-only transaction ownership; never owns or disposes an injected context.</summary>
/// <typeparam name="TContext">The scoped write context.</typeparam>
/// <param name="context">The write context shared by repositories.</param>
public sealed class UnitOfWork<TContext>(TContext context) : IUnitOfWork, IDisposable, IAsyncDisposable
    where TContext : ScopedDbContext
{
    private IDbContextTransaction? _transaction;

    /// <summary>Begins an explicitly owned EF-only transaction.</summary>
    /// <param name="isolationLevel">Database isolation.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The owned transaction.</returns>
    /// <exception cref="InvalidOperationException">Transaction ownership cannot be acquired.</exception>
    public async Task<IDbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default)
    {
        if (context.ReadOnly || context.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("Cannot acquire transaction ownership.");
        }

        _transaction = await context.Database.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);
        return _transaction.GetDbTransaction();
    }

    /// <summary>Commits this helper's own transaction.</summary>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The operation task.</returns>
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = RequireOwnedTransaction();
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        await transaction.DisposeAsync().ConfigureAwait(false);
        _transaction = null;
    }

    /// <summary>Rollbacks this helper's own transaction.</summary>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The operation task.</returns>
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = RequireOwnedTransaction();
        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        await transaction.DisposeAsync().ConfigureAwait(false);
        _transaction = null;
        context.ChangeTracker.Clear();
    }

    /// <inheritdoc/>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnsureNoPendingEvents();
        return context.Database.CurrentTransaction is not null && context.Database.CurrentTransaction != _transaction
            ? throw new InvalidOperationException("The active transaction is owned by another pipeline.")
            : context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _transaction?.Dispose();
        _transaction = null;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = null;
        }
    }

    private void EnsureNoPendingEvents()
    {
        if (context.ChangeTracker.Entries<IHasDomainEvents>().Any(entry => entry.Entity.DomainEvents.Count != 0))
        {
            throw new InvalidOperationException("Pending domain events require Wolverine transaction/outbox ownership.");
        }
    }

    private IDbContextTransaction RequireOwnedTransaction() => _transaction
        ?? throw new InvalidOperationException("This unit of work does not own a transaction.");
}
