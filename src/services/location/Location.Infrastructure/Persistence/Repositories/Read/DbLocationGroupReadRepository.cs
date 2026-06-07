// <copyright file="DbLocationGroupReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Location.Application.Service.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Location.Infrastructure.Persistence.Repositories.Read;

public sealed class DbLocationGroupReadRepository(LocationReadDbContext dbContext) : ILocationGroupReadRepository
{
    private readonly LocationReadDbContext dbContext = dbContext;

    public async ValueTask<LocationGroupSnapshot?> GetByIdAsync(
        string tenantId,
        string locationGroupId,
        CancellationToken cancellationToken)
    {
        LocationGroupRecord? record = await this.dbContext.LocationGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(
                group => group.TenantId == tenantId && group.LocationGroupId == locationGroupId,
                cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return null;
        }

        return new LocationGroupSnapshot(
            record.TenantId,
            record.LocationGroupId,
            record.Name);
    }

    public async ValueTask<IReadOnlyList<LocationGroupSnapshot>> ListByTenantAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        List<LocationGroupRecord> records = await this.dbContext.LocationGroups
            .AsNoTracking()
            .Where(group => group.TenantId == tenantId)
            .OrderBy(group => group.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return records.Select(record => new LocationGroupSnapshot(record.TenantId, record.LocationGroupId, record.Name)).ToList();
    }
}
