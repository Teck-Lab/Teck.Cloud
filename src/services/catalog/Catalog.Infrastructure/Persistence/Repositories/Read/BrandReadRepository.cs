using Catalog.Application.Brands.ReadModels;
using Catalog.Application.Brands.Repositories;
using SharedKernel.Persistence.Database.EFCore;

namespace Catalog.Infrastructure.Persistence.Repositories.Read;

/// <summary>
/// Repository for read operations on Brand entities.
/// </summary>
public sealed class BrandReadRepository : GenericReadRepository<BrandReadModel, Guid, ApplicationReadDbContext>, IBrandReadRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BrandReadRepository"/> class.
    /// </summary>
    /// <param name="readDbContext">The read database context.</param>
    public BrandReadRepository(ApplicationReadDbContext readDbContext)
        : base(readDbContext)
    {
    }

    /// <inheritdoc/>
    public async Task<BrandReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await FindByIdAsync(id, cancellationToken: cancellationToken);
    }
}
