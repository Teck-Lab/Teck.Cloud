using Catalog.Domain.Entities.CategoryAggregate;
using Catalog.Domain.Entities.CategoryAggregate.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence.Database.EFCore;

namespace Catalog.Infrastructure.Persistence.Repositories.Write;

/// <summary>
/// Repository for write operations on Category entities.
/// </summary>
public sealed class CategoryWriteRepository : GenericWriteRepository<Category, Guid, ApplicationWriteDbContext>, ICategoryWriteRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CategoryWriteRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public CategoryWriteRepository(
        ApplicationWriteDbContext dbContext,
        IHttpContextAccessor httpContextAccessor)
        : base(dbContext, httpContextAccessor)
    {
    }

    /// <inheritdoc/>
    public async Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await DbContext.Categories
            .FirstOrDefaultAsync(category => category.Name == name, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Category>> GetByParentIdAsync(Guid parentId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Categories
            .Where(category => EF.Property<Guid?>(category, "ParentId") == parentId)
            .ToListAsync(cancellationToken);
    }

    // Use the base implementation of FirstOrDefaultAsync; when tracked entity is required,
    // call the overload with `enableTracking: true` in the callers below.
}
