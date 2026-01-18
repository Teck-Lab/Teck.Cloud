using Catalog.Application.Categories.ReadModels;
using Catalog.Application.Categories.Repositories;
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
    /// <param name="categoryCache"></param>
    /// <param name="categoryReadRepository">The category repository.</param>
    public sealed class CategoryCache(IFusionCache categoryCache, ICategoryReadRepository categoryReadRepository) : GenericCacheService<CategoryReadModel, Guid>(categoryCache, categoryReadRepository), ICategoryCache
    {
    }
}
