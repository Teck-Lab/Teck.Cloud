using Catalog.Application.Suppliers.ReadModels;
using SharedKernel.Core.Database;
using SharedKernel.Core.Pagination;

namespace Catalog.Application.Suppliers.Repositories;

/// <summary>
/// Repository interface for read operations on Supplier entities.
/// </summary>
public interface ISupplierReadRepository : IGenericReadRepository<SupplierReadModel, Guid>
{
    /// <summary>
    /// Gets all suppliers.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of supplier read models.</returns>
    Task<IReadOnlyList<SupplierReadModel>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a supplier by ID.
    /// </summary>
    /// <param name="id">The supplier ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The supplier read model if found, otherwise null.</returns>
    Task<SupplierReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get paged suppliers.
    /// </summary>
    /// <param name="page">The page number.</param>
    /// <param name="size">The page size.</param>
    /// <param name="keyword">The search keyword.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A paged list of supplier read models.</returns>
    Task<PagedList<SupplierReadModel>> GetPagedSuppliersAsync(int page, int size, string? keyword, CancellationToken cancellationToken = default);
}
