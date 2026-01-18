using Catalog.Application.Products.ReadModels;
using SharedKernel.Core.Caching;

namespace Catalog.Application.Products.Repositories
{
    /// <summary>
    /// Product cache interface.
    /// </summary>
    public interface IProductCache : IGenericCacheService<ProductReadModel, Guid>
    {
    }
}
