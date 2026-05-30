// <copyright file="DbDeviceDefinitionReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceDefinitions.Abstractions;
using Device.Application.DeviceDefinitions.ReadModels;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.Devices;
using SharedKernel.Core.Pagination;

namespace Device.Infrastructure.Persistence.Repositories.Read;

internal sealed class DbDeviceDefinitionReadRepository(
    IDbContextFactory<DeviceReadDbContext> dbContextFactory)
    : IDeviceDefinitionReadRepository
{
    private readonly IDbContextFactory<DeviceReadDbContext> dbContextFactory = dbContextFactory;

    /// <inheritdoc/>
    public async ValueTask<DeviceDefinitionSnapshot?> GetByModelIdAsync(string modelId, CancellationToken cancellationToken)
    {
        await using DeviceReadDbContext dbContext = await this.dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        DeviceDefinitionReadModel? record = await dbContext.DeviceDefinitions
            .AsNoTracking()
            .SingleOrDefaultAsync(definition => definition.ModelId == modelId, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : MapToSnapshot(record);
    }

    /// <inheritdoc/>
    public async ValueTask<DeviceDefinitionSnapshot?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using DeviceReadDbContext dbContext = await this.dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        DeviceDefinitionReadModel? record = await dbContext.DeviceDefinitions
            .AsNoTracking()
            .SingleOrDefaultAsync(definition => definition.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : MapToSnapshot(record);
    }

    /// <inheritdoc/>
    public async Task<PagedList<DeviceDefinitionSnapshot>> GetPagedAsync(
        int page,
        int size,
        string? sortBy,
        bool sortDescending,
        CancellationToken cancellationToken)
    {
        await using DeviceReadDbContext dbContext = await this.dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        IQueryable<DeviceDefinitionReadModel> query = dbContext.DeviceDefinitions.AsNoTracking();

        query = (sortBy?.ToLowerInvariant(), sortDescending) switch
        {
            ("name", false) => query.OrderBy(definition => definition.Name),
            ("name", true) => query.OrderByDescending(definition => definition.Name),
            ("eslprovider", false) => query.OrderBy(definition => definition.EslProvider),
            ("eslprovider", true) => query.OrderByDescending(definition => definition.EslProvider),
            (_, false) => query.OrderBy(definition => definition.ModelId),
            (_, true) => query.OrderByDescending(definition => definition.ModelId),
        };

        int totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        List<DeviceDefinitionReadModel> records = await query
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<DeviceDefinitionSnapshot> items = records
            .Select(MapToSnapshot)
            .ToList();

        return new PagedList<DeviceDefinitionSnapshot>(items, totalCount, page, size);
    }

    private static DeviceDefinitionSnapshot MapToSnapshot(DeviceDefinitionReadModel model)
    {
        return new DeviceDefinitionSnapshot(
            model.Id,
            model.ModelId,
            model.Name,
            model.WidthPx,
            model.HeightPx,
            (Device.Domain.Entities.DeviceDefinitionAggregate.DisplayInkColor)model.SupportedColors,
            model.SupportsNfc,
            EslProvider.FromName(model.EslProvider, false),
            model.CatalogManufacturerId,
            model.CatalogSupplierId,
            model.CatalogProductId);
    }
}
