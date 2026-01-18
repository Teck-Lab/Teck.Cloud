using SharedKernel.Core.Database;

namespace Catalog.Domain.Entities.SupplierAggregate.Repositories;

/// <summary>
/// Repository interface for write operations on Supplier entities.
/// </summary>
public interface ISupplierWriteRepository : IGenericWriteRepository<Supplier, Guid>
{
    /// <summary>
    /// Gets a supplier by name.
    /// </summary>
    /// <param name="name">The name of the supplier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The supplier if found, otherwise null.</returns>
    Task<Supplier?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}
