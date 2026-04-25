// <copyright file="TenantWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

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
    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await this.DbContext.Tenants
            .Include(tenant => tenant.Databases)
            .Where(tenant => tenant.Id == id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a tenant by its Keycloak organization identifier.
    /// </summary>
    /// <param name="identifier">The Keycloak organization identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tenant if found; otherwise, null.</returns>
    public async Task<Tenant?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken)
    {
        return await this.DbContext.Tenants
            .Include(tenant => tenant.Databases)
            .Where(tenant => tenant.KeycloakOrganizationId == identifier)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if a tenant with the specified identifier exists.
    /// </summary>
    /// <param name="identifier">The tenant identifier to check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if a tenant with the identifier exists; otherwise, false.</returns>
    public async Task<bool> ExistsByIdentifierAsync(string identifier, CancellationToken cancellationToken)
    {
        return await this.DbContext.Tenants
            .AnyAsync(tenant => tenant.Identifier == identifier, cancellationToken)
            .ConfigureAwait(false);
    }
}
