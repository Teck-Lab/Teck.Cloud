// <copyright file="LicenseWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Domain.Entities.LicenseAggregate;
using Customer.Domain.Entities.LicenseAggregate.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence.Database.EFCore;

namespace Customer.Infrastructure.Persistence.Repositories.Write;

/// <summary>
/// Repository for write operations on License entities.
/// </summary>
public sealed class LicenseWriteRepository : GenericWriteRepository<License, Guid, CustomerWriteDbContext>, ILicenseWriteRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LicenseWriteRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public LicenseWriteRepository(
        CustomerWriteDbContext dbContext,
        IHttpContextAccessor httpContextAccessor)
        : base(dbContext, httpContextAccessor)
    {
    }

    /// <inheritdoc/>
    public async Task<License?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await this.DbContext.Licenses
            .Where(license => license.Id == id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<License?> GetActiveByTenantIdAsync(string tenantId, CancellationToken cancellationToken)
    {
        return await this.DbContext.Licenses
            .Where(license => license.TenantId == tenantId && license.Status.IsUsable)
            .OrderByDescending(license => license.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<License?> GetActiveByLocationIdAsync(string locationId, CancellationToken cancellationToken)
    {
        return await this.DbContext.Licenses
            .Where(license => license.LocationId == locationId && license.Status.IsUsable)
            .OrderByDescending(license => license.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<License>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken)
    {
        return await this.DbContext.Licenses
            .Where(license => license.TenantId == tenantId)
            .OrderByDescending(license => license.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(License license, CancellationToken cancellationToken)
    {
        this.DbContext.Licenses.Update(license);
        await this.DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
