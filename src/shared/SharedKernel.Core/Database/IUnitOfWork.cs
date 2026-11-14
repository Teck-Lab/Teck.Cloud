namespace SharedKernel.Core.Database;

/// <summary>Application save boundary shared by repositories and command handlers.</summary>
/// <remarks>
/// Saving participates in the active transaction. It does not independently commit
/// a Wolverine-owned handler transaction. Infrastructure owns transaction and context lifetimes.
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>Saves pending changes, coordinating durable events through the configured persistence integration.</summary>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The number of state entries written, including tracked infrastructure entries.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
