using SharedKernel.Core.Database;

namespace Catalog.Domain.Entities.ProductAggregate.Repositories;

/// <summary>
/// Repository interface for write operations on Product entities.
/// </summary>
public interface IProductWriteRepository : IGenericWriteRepository<Product, Guid>
{
    /// <summary>
    /// Gets a product by SKU.
    /// </summary>
    /// <param name="sku">The SKU of the product.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The product if found, otherwise null.</returns>
    Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);
}
