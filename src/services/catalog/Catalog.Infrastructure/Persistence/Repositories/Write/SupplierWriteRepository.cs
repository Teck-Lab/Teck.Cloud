using Catalog.Domain.Entities.SupplierAggregate;
using Catalog.Domain.Entities.SupplierAggregate.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence.Database.EFCore;

namespace Catalog.Infrastructure.Persistence.Repositories.Write;

/// <summary>
/// Repository for write operations on Supplier entities.
/// </summary>
public sealed class SupplierWriteRepository : GenericWriteRepository<Supplier, Guid, ApplicationWriteDbContext>, ISupplierWriteRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SupplierWriteRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public SupplierWriteRepository(
        ApplicationWriteDbContext dbContext,
        IHttpContextAccessor httpContextAccessor)
        : base(dbContext, httpContextAccessor)
    {
    }

    /// <inheritdoc/>
    public async Task<Supplier?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await DbContext.Suppliers
            .FirstOrDefaultAsync(supplier => supplier.Name == name, cancellationToken);
    }

    // Use the base implementation of FirstOrDefaultAsync; when tracked entity is required,
    // call the overload with `enableTracking: true` in the callers below.
}
