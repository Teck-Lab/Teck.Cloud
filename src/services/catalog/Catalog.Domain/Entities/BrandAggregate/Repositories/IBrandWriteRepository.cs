using SharedKernel.Core.Database;

namespace Catalog.Domain.Entities.BrandAggregate.Repositories;

/// <summary>
/// Repository interface for write operations on Brand entities.
/// </summary>
public interface IBrandWriteRepository : IGenericWriteRepository<Brand, Guid>
{
    /// <summary>
    /// Checks if a brand with the specified name exists.
    /// </summary>
    /// <param name="name">The brand name to check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if a brand with the name exists; otherwise, false.</returns>
    Task<bool> ExistsWithNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a brand by its name.
    /// </summary>
    /// <param name="name">The brand name to find.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The brand if found; otherwise, null.</returns>
    Task<Brand?> FindByNameAsync(string name, CancellationToken cancellationToken = default);
}
