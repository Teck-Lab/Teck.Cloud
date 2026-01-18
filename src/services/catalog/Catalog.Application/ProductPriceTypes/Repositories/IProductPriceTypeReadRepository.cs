using Catalog.Application.ProductPriceTypes.ReadModels;
using SharedKernel.Core.Database;
using SharedKernel.Core.Pagination;

namespace Catalog.Application.ProductPriceTypes.Repositories;

/// <summary>
/// Repository interface for read operations on ProductPriceType entities.
/// </summary>
public interface IProductPriceTypeReadRepository : IGenericReadRepository<ProductPriceTypeReadModel, Guid>
{
    /// <summary>
    /// Gets all product price types.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of product price type read models.</returns>
    Task<IReadOnlyList<ProductPriceTypeReadModel>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a product price type by ID.
    /// </summary>
    /// <param name="id">The product price type ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The product price type read model if found, otherwise null.</returns>
    Task<ProductPriceTypeReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get paged product price types.
    /// </summary>
    /// <param name="page">The page number.</param>
    /// <param name="size">The page size.</param>
    /// <param name="keyword">The search keyword.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A paged list of product price type read models.</returns>
    Task<PagedList<ProductPriceTypeReadModel>> GetPagedProductPriceTypesAsync(int page, int size, string? keyword, CancellationToken cancellationToken = default);
}
