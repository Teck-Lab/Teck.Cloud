using Catalog.Application.Brands.ReadModels;
using SharedKernel.Core.Caching;

namespace Catalog.Application.Brands.Repositories
{
    /// <summary>
    /// Brand cache interface for read models.
    /// Provides high-performance access to brand data for query operations.
    /// Write operations will update this cache after persisting changes to the database.
    /// </summary>
    public interface IBrandCache : IGenericCacheService<BrandReadModel, Guid>
    {
    }
}
