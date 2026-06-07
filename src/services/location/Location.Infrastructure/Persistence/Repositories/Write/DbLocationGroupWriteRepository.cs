// <copyright file="DbLocationGroupWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Location.Application.Service.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Location.Infrastructure.Persistence.Repositories.Write;

public sealed class DbLocationGroupWriteRepository(TemplateFontMetadataDbContext dbContext) : ILocationGroupWriteRepository
{
    private readonly TemplateFontMetadataDbContext dbContext = dbContext;

    public async ValueTask UpsertAsync(LocationGroupSnapshot snapshot, CancellationToken cancellationToken)
    {
        LocationGroupRecord? existing = await this.dbContext.LocationGroups
            .FirstOrDefaultAsync(
                group => group.TenantId == snapshot.TenantId && group.LocationGroupId == snapshot.LocationGroupId,
                cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (existing is not null)
        {
            existing.Name = snapshot.Name;
            existing.UpdatedAtUtc = now;
            this.dbContext.LocationGroups.Update(existing);
        }
        else
        {
            this.dbContext.LocationGroups.Add(new LocationGroupRecord
            {
                Id = Guid.NewGuid(),
                TenantId = snapshot.TenantId,
                LocationGroupId = snapshot.LocationGroupId,
                Name = snapshot.Name,
                UpdatedAtUtc = now,
            });
        }

        await this.dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DeleteAsync(string tenantId, string locationGroupId, CancellationToken cancellationToken)
    {
        LocationGroupRecord? existing = await this.dbContext.LocationGroups
            .FirstOrDefaultAsync(
                group => group.TenantId == tenantId && group.LocationGroupId == locationGroupId,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            this.dbContext.LocationGroups.Remove(existing);
            await this.dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
