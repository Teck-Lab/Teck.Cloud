using Catalog.Application.Categories.ReadModels;
using SharedKernel.Core.Caching;

namespace Catalog.Application.Categories.Repositories;

/// <summary>
/// Provides caching operations for <see cref="CategoryReadModel"/> entities.
/// </summary>
public interface ICategoryCache : IGenericCacheService<CategoryReadModel, Guid>
{
}
