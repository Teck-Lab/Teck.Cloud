using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SharedKernel.Core.Domain;
using SharedKernel.Core.MultiTenancy;

namespace SharedKernel.Persistence.Database;

/// <summary>Applies opt-in auditing and soft deletion without depending on HTTP.</summary>
/// <param name="operation">The scoped actor identity.</param>
/// <param name="clock">The application clock.</param>
public sealed class AuditAndSoftDeleteInterceptor(OperationContext operation, TimeProvider clock) : SaveChangesInterceptor
{
    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return result;
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Apply(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries().ToArray())
        {
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable deleted &&
                (context is not ScopedDbContext scoped || !scoped.IsHardDelete(entry.Entity)))
            {
                entry.State = EntityState.Modified;
                deleted.SetDeletedProperties(isDeleted: true, operation.ActorId, clock.GetUtcNow());
            }

            if (entry.Entity is IAuditable audit)
            {
                if (entry.State == EntityState.Added)
                {
                    audit.SetCreatedByProperties(operation.ActorId);
                }
                else if (entry.State == EntityState.Modified)
                {
                    audit.SetUpdatedProperties(clock.GetUtcNow(), operation.ActorId);
                }
            }
        }
    }
}
