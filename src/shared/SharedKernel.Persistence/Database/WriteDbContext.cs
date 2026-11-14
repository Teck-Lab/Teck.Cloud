using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Database;
using SharedKernel.Core.MultiTenancy;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Runtime;

namespace SharedKernel.Persistence.Database;

/// <summary>Application unit of work over the actual scoped write context and Wolverine's official outbox.</summary>
/// <param name="options">Provider and model configuration.</param>
/// <param name="operation">Validated operation identity.</param>
/// <param name="outbox">Scoped official outbox for saves outside a handler transaction.</param>
/// <param name="messaging">The active scoped Wolverine message context.</param>
/// <param name="clock">Optional audit clock.</param>
public abstract class WriteDbContext(DbContextOptions options, OperationContext operation, IDbContextOutbox outbox, IMessageContext messaging, TimeProvider? clock = null)
    : ScopedDbContext(options, operation, readOnly: false, clock), IUnitOfWork
{
    private int _lastSavedEntries;

    /// <inheritdoc/>
    async Task<int> IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        _ = Scope;
        if (messaging.Envelope is not null)
        {
            if (Database.CurrentTransaction is null)
            {
                throw new InvalidOperationException("Handler unit-of-work saves require Wolverine Eager transaction ownership.");
            }

            // Eager Wolverine middleware owns commit and domain-event collection.
            // A handler may save here and still roll back if later work fails.
            return await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("Outside handlers, use the official outbox explicitly for a caller-owned transaction.");
        }

        // Ordinary scoped application code uses the same context, with official
        // domain-event scraping and atomic outbox persistence before delivery.
        outbox.Enroll(this);
        outbox.TenantId = Scope.TenantId;
        await outbox.SaveChangesAndFlushMessagesAsync(MultiFlushMode.AllowMultiples, cancellationToken).ConfigureAwait(false);
        return _lastSavedEntries;
    }

    /// <inheritdoc/>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        _lastSavedEntries = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false);
        return _lastSavedEntries;
    }
}
