using Catalog.Domain.Entities.ProductAggregate;
using Catalog.Domain.Entities.ProductAggregate.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence.Database.EFCore;

namespace Catalog.Infrastructure.Persistence.Repositories.Write;

/// <summary>
/// Repository for write operations on Product entities.
/// </summary>
public sealed class ProductWriteRepository : GenericWriteRepository<Product, Guid, ApplicationWriteDbContext>, IProductWriteRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProductWriteRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public ProductWriteRepository(
        ApplicationWriteDbContext dbContext,
        IHttpContextAccessor httpContextAccessor)
        : base(dbContext, httpContextAccessor)
    {
    }

    /// <inheritdoc/>
    public async Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        return await DbContext.Products
            .FirstOrDefaultAsync(product => product.ProductSKU == sku, cancellationToken);
    }

    // Use the base implementation of FirstOrDefaultAsync; when tracked entity is required,
    // call the overload with `enableTracking: true` in the callers below.
}
