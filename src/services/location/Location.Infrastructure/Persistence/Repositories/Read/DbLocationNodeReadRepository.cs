// <copyright file="DbLocationNodeReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Location.Application.Service.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Location.Infrastructure.Persistence.Repositories.Read;

internal sealed class DbLocationNodeReadRepository(
    IDbContextFactory<LocationReadDbContext> dbContextFactory)
    : ILocationNodeReadRepository
{
    private readonly IDbContextFactory<LocationReadDbContext> dbContextFactory = dbContextFactory;

    public async ValueTask<LocationNodeSnapshot?> GetByIdAsync(string locationNodeId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(locationNodeId))
        {
            return null;
        }

        string normalizedLocationNodeId = locationNodeId.Trim();

        await using LocationReadDbContext dbContext = await this.dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        LocationNodeRecord? record = await dbContext.LocationNodes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                node => node.LocationNodeId == normalizedLocationNodeId,
                cancellationToken)
            .ConfigureAwait(false);

        return record is not null
            ? new LocationNodeSnapshot(record.LocationNodeId, record.ParentLocationNodeId, record.TemplateId, record.Name, record.LocationGroupId, record.Aisle, record.Shelf)
            : null;
    }

    public async ValueTask<IReadOnlyList<LocationNodeSnapshot>> SearchByNameAsync(string? query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using LocationReadDbContext dbContext = await this.dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        IQueryable<LocationNodeRecord> dbQuery = dbContext.LocationNodes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query))
        {
            string trimmed = query.Trim();
            dbQuery = dbQuery.Where(node => node.Name != null && EF.Functions.Like(node.Name, $"%{trimmed}%"));
        }

        List<LocationNodeSnapshot> records = await dbQuery
            .OrderBy(node => node.Name)
            .ThenBy(node => node.LocationNodeId)
            .Select(node => new LocationNodeSnapshot(node.LocationNodeId, node.ParentLocationNodeId, node.TemplateId, node.Name, node.LocationGroupId, node.Aisle, node.Shelf))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return records;
    }
}
