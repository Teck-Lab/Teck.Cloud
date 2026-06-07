// <copyright file="DbDeviceLayoutReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceLayouts.Abstractions;
using Device.Application.DeviceLayouts.ReadModels;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Pagination;

namespace Device.Infrastructure.Persistence.Repositories.Read;

public sealed class DbDeviceLayoutReadRepository(
    IDbContextFactory<DeviceReadDbContext> dbContextFactory)
    : IDeviceLayoutReadRepository
{
    private readonly IDbContextFactory<DeviceReadDbContext> dbContextFactory = dbContextFactory;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DeviceLayoutSnapshot>> GetByDeviceDefinitionIdAsync(
        Guid deviceDefinitionId,
        CancellationToken cancellationToken)
    {
        await using DeviceReadDbContext dbContext = await this.dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        List<DeviceLayoutReadModel> records = await dbContext.DeviceLayouts
            .AsNoTracking()
            .Where(layout => layout.DeviceDefinitionId == deviceDefinitionId)
            .OrderBy(layout => layout.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return records
            .Select(MapToSnapshot)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<PagedList<DeviceLayoutSnapshot>> GetPagedAsync(
        int page,
        int size,
        CancellationToken cancellationToken)
    {
        await using DeviceReadDbContext dbContext = await this.dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        IQueryable<DeviceLayoutReadModel> query = dbContext.DeviceLayouts
            .AsNoTracking()
            .OrderBy(layout => layout.Name);

        int totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        List<DeviceLayoutReadModel> records = await query
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<DeviceLayoutSnapshot> items = records
            .Select(MapToSnapshot)
            .ToList();

        return new PagedList<DeviceLayoutSnapshot>(items, totalCount, page, size);
    }

    private static DeviceLayoutSnapshot MapToSnapshot(DeviceLayoutReadModel model) =>
        new(model.Id, model.DeviceDefinitionId, model.Name, model.MaxZoneCount);
}
