using Catalog.Application.Brands.ReadModels;
using Catalog.Application.Brands.Repositories;
using SharedKernel.Persistence.Caching;
using ZiggyCreatures.Caching.Fusion;

namespace Catalog.Infrastructure.Caching
{
    /// <summary>
    /// The brand cache.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="BrandCache"/> class.
    /// </remarks>
    /// <param name="brandCache"></param>
    /// <param name="brandReadRepository">The brand repository.</param>
    public sealed class BrandCache(IFusionCache brandCache, IBrandReadRepository brandReadRepository) : GenericCacheService<BrandReadModel, Guid>(brandCache, brandReadRepository), IBrandCache
    {
    }
}
