using Catalog.Domain.Entities.ProductPriceTypeAggregate;
using Catalog.Domain.Entities.ProductPriceTypeAggregate.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence.Database.EFCore;

namespace Catalog.Infrastructure.Persistence.Repositories.Write;

/// <summary>
/// Repository for write operations on ProductPriceType entities.
/// </summary>
public sealed class ProductPriceTypeWriteRepository : GenericWriteRepository<ProductPriceType, Guid, ApplicationWriteDbContext>, IProductPriceTypeWriteRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProductPriceTypeWriteRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public ProductPriceTypeWriteRepository(
        ApplicationWriteDbContext dbContext,
        IHttpContextAccessor httpContextAccessor)
        : base(dbContext, httpContextAccessor)
    {
    }

    /// <inheritdoc/>
    public async Task<ProductPriceType?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await DbContext.ProductPriceTypes
            .FirstOrDefaultAsync(productPriceType => productPriceType.Name == name, cancellationToken);
    }

    // Use the base implementation of FirstOrDefaultAsync; when tracked entity is required,
    // call the overload with `enableTracking: true` in the callers below.
}
