// <copyright file="DbDisplayModelReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Location.Application.Service.Abstractions;
using Location.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Location.Infrastructure.Service;

public sealed class DbDisplayModelReadRepository(
    IDbContextFactory<TemplateFontMetadataDbContext> dbContextFactory)
    : IDisplayModelReadRepository
{
    private readonly IDbContextFactory<TemplateFontMetadataDbContext> dbContextFactory = dbContextFactory;

    public async ValueTask<IReadOnlyList<DisplayModelSnapshot>> ListAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return [];
        }

        string normalizedTenantId = tenantId.Trim();

        await using TemplateFontMetadataDbContext dbContext = await this.dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        DisplayModelRecord[] records = await dbContext.DisplayModels
            .AsNoTracking()
            .Where(record =>
                record.TenantId == normalizedTenantId ||
                record.TenantId == DisplayModelSeedData.SharedTenantId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        List<DisplayModelSnapshot> snapshots = records
            .GroupBy(record => record.DisplayModelId, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(record => string.Equals(record.TenantId, normalizedTenantId, StringComparison.Ordinal))
                .ThenByDescending(record => record.UpdatedAtUtc)
                .First())
            .OrderBy(record => record.Name, StringComparer.Ordinal)
            .Select(static record => new DisplayModelSnapshot(
                record.DisplayModelId,
                record.Name,
                record.Width,
                record.Height))
            .ToList();

        return snapshots;
    }
}
