using Catalog.Application.Products.ReadModels;
using SharedKernel.Core.Database;
using SharedKernel.Core.Pagination;

namespace Catalog.Application.Products.Repositories;

/// <summary>
/// Repository interface for read operations on Product entities.
/// </summary>
public interface IProductReadRepository : IGenericReadRepository<ProductReadModel, Guid>
{
    /// <summary>
    /// Gets all products.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of product read models.</returns>
    Task<IReadOnlyList<ProductReadModel>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a product by ID.
    /// </summary>
    /// <param name="id">The product ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The product read model if found, otherwise null.</returns>
    Task<ProductReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets products by brand ID.
    /// </summary>
    /// <param name="brandId">The brand ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of product read models.</returns>
    Task<IReadOnlyList<ProductReadModel>> GetByBrandIdAsync(Guid brandId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets products by category ID.
    /// </summary>
    /// <param name="categoryId">The category ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of product read models.</returns>
    Task<IReadOnlyList<ProductReadModel>> GetByCategoryIdAsync(Guid categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get paged products.
    /// </summary>
    /// <param name="page">The page number.</param>
    /// <param name="size">The page size.</param>
    /// <param name="keyword">The search keyword.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A paged list of product read models.</returns>
    Task<PagedList<ProductReadModel>> GetPagedProductsAsync(int page, int size, string? keyword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a product by SKU.
    /// </summary>
    /// <param name="sku">The product SKU.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The product read model if found, otherwise null.</returns>
    Task<ProductReadModel?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);
}
