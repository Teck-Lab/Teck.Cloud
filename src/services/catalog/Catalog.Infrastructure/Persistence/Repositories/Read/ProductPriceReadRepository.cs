using Catalog.Application.Products.ReadModels;
using Catalog.Application.Products.Repositories;
using SharedKernel.Persistence.Database.EFCore;

namespace Catalog.Infrastructure.Persistence.Repositories.Read;

/// <summary>
/// Repository for ProductPrice read operations.
/// </summary>
public sealed class ProductPriceReadRepository : GenericReadRepository<ProductPriceReadModel, Guid, ApplicationReadDbContext>, IProductPriceReadRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProductPriceReadRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public ProductPriceReadRepository(
        ApplicationReadDbContext dbContext)
        : base(dbContext)
    {
    }
}
