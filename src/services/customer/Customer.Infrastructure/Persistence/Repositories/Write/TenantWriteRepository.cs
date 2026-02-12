using Customer.Domain.Entities.TenantAggregate;
using Customer.Domain.Entities.TenantAggregate.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence.Database.EFCore;

namespace Customer.Infrastructure.Persistence.Repositories.Write;

/// <summary>
/// Repository for write operations on Tenant entities.
/// </summary>
public sealed class TenantWriteRepository : GenericWriteRepository<Tenant, Guid, CustomerWriteDbContext>, ITenantWriteRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TenantWriteRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public TenantWriteRepository(
        CustomerWriteDbContext dbContext,
        IHttpContextAccessor httpContextAccessor)
        : base(dbContext, httpContextAccessor)
    {
    }

    /// <summary>
    /// Gets a tenant by its identifier, including related entities.
    /// </summary>
    /// <param name="id">The tenant identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tenant if found; otherwise, null.</returns>
    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Tenants
            .Where(tenant => tenant.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Gets a tenant by its identifier (unique slug).
    /// </summary>
    /// <param name="identifier">The tenant identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tenant if found; otherwise, null.</returns>
    public async Task<Tenant?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        return await DbContext.Tenants
            .Where(tenant => tenant.Identifier == identifier)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Checks if a tenant with the specified identifier exists.
    /// </summary>
    /// <param name="identifier">The tenant identifier to check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if a tenant with the identifier exists; otherwise, false.</returns>
    public async Task<bool> ExistsByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        return await DbContext.Tenants
            .AnyAsync(tenant => tenant.Identifier == identifier, cancellationToken);
    }
}
