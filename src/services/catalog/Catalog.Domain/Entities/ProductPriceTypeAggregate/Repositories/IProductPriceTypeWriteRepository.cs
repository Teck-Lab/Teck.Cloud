using SharedKernel.Core.Database;

namespace Catalog.Domain.Entities.ProductPriceTypeAggregate.Repositories;

/// <summary>
/// Repository interface for write operations on ProductPriceType entities.
/// </summary>
public interface IProductPriceTypeWriteRepository : IGenericWriteRepository<ProductPriceType, Guid>
{
    /// <summary>
    /// Gets a product price type by name.
    /// </summary>
    /// <param name="name">The name of the product price type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The product price type if found, otherwise null.</returns>
    Task<ProductPriceType?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}
