using Catalog.Application.Products.ReadModels;
using Catalog.Application.Products.Repositories;
using SharedKernel.Persistence.Caching;
using ZiggyCreatures.Caching.Fusion;

namespace Catalog.Infrastructure.Caching
{
    /// <summary>
    /// The product cache.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ProductCache"/> class.
    /// </remarks>
    /// <param name="brandCache">The brand cache.</param>
    /// <param name="productReadRepository">The brand repository.</param>
    public sealed class ProductCache(IFusionCache brandCache, IProductReadRepository productReadRepository) : GenericCacheService<ProductReadModel, Guid>(brandCache, productReadRepository), IProductCache
    {
    }
}
