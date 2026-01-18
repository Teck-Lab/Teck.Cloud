using Catalog.Domain.Entities.BrandAggregate;
using Catalog.Domain.Entities.BrandAggregate.Repositories;
using Catalog.Domain.Entities.BrandAggregate.Specifications;
using Microsoft.AspNetCore.Http;
using SharedKernel.Persistence.Database.EFCore;

namespace Catalog.Infrastructure.Persistence.Repositories.Write;

/// <summary>
/// Repository for write operations on Brand entities.
/// </summary>
public sealed class BrandWriteRepository : GenericWriteRepository<Brand, Guid, ApplicationWriteDbContext>, IBrandWriteRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BrandWriteRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public BrandWriteRepository(
        ApplicationWriteDbContext dbContext,
        IHttpContextAccessor httpContextAccessor)
        : base(dbContext, httpContextAccessor)
    {
    }

    // Use the base implementation of FirstOrDefaultAsync; when tracked entity is required,
    // call the overload with `enableTracking: true` in the callers below.

    /// <summary>
    /// Checks if a brand with the specified name exists.
    /// </summary>
    /// <param name="name">The brand name to check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if a brand with the name exists; otherwise, false.</returns>
    public async Task<bool> ExistsWithNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await FirstOrDefaultAsync(new BrandByNameSpecification(name), cancellationToken) != null;
    }

    /// <summary>
    /// Finds a brand by its name.
    /// </summary>
    /// <param name="name">The brand name to find.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The brand if found; otherwise, null.</returns>
    public async Task<Brand?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await FirstOrDefaultAsync(new BrandByNameSpecification(name), cancellationToken);
    }
}
