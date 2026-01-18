using Catalog.Application.Brands.ReadModels;
using SharedKernel.Core.Database;

namespace Catalog.Application.Brands.Repositories;

/// <summary>
/// Repository interface for read operations on Brand entities.
/// </summary>
public interface IBrandReadRepository : IGenericReadRepository<BrandReadModel, Guid>
{
    /// <summary>
    /// Gets a brand by ID.
    /// </summary>
    /// <param name="id">The brand ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The brand read model if found, otherwise null.</returns>
    Task<BrandReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
