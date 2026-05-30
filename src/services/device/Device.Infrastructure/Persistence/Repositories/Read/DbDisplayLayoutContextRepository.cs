// <copyright file="DbDisplayLayoutContextRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.Abstractions;
using Device.Application.DeviceLayouts.ReadModels;
using Device.Domain.Entities.DisplayAggregate;
using Microsoft.EntityFrameworkCore;

namespace Device.Infrastructure.Persistence.Repositories.Read;

/// <summary>
/// Resolves the layout context for a display — used by the assignment handler to
/// determine zone-count constraints for label rendering.
/// </summary>
internal sealed class DbDisplayLayoutContextRepository(
    IDbContextFactory<DeviceReadDbContext> dbContextFactory)
    : IDeviceDefinitionReadRepository
{
    private readonly IDbContextFactory<DeviceReadDbContext> dbContextFactory = dbContextFactory;

    /// <inheritdoc/>
    public async ValueTask<DisplayLayoutContext?> GetLayoutContextByDisplayIdAsync(
        Guid displayId,
        CancellationToken cancellationToken)
    {
        await using DeviceReadDbContext dbContext = await this.dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Display? display = await dbContext.Displays
            .AsNoTracking()
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(display => display.Id == displayId, cancellationToken)
            .ConfigureAwait(false);

        if (display is null || display.DeviceLayoutId is null)
        {
            return null;
        }

        DeviceLayoutReadModel? layout = await dbContext.DeviceLayouts
            .AsNoTracking()
            .SingleOrDefaultAsync(layout => layout.Id == display.DeviceLayoutId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (layout is null)
        {
            return null;
        }

        return new DisplayLayoutContext(display.Id, layout.Id, layout.MaxZoneCount);
    }
}
