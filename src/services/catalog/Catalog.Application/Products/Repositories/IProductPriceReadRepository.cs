using Catalog.Application.Products.ReadModels;
using SharedKernel.Core.Database;

namespace Catalog.Application.Products.Repositories;

/// <summary>
/// Interface for ProductPrice read operations.
/// </summary>
public interface IProductPriceReadRepository : IGenericReadRepository<ProductPriceReadModel, Guid>
{
}
