// <copyright file="DbDisplayReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Displays.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Device.Infrastructure.Persistence.Repositories.Read;

internal sealed class DbDisplayReadRepository(DeviceReadDbContext dbContext) : IDisplayReadRepository
{
    private readonly DeviceReadDbContext dbContext = dbContext;

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<DisplaySnapshot>> GetByLocationAsync(
        string locationNodeId,
        CancellationToken cancellationToken)
    {
        List<DisplaySnapshot> results = await this.dbContext.Displays
            .AsNoTracking()
            .Where(display => display.LocationNodeId == locationNodeId)
            .OrderBy(display => display.ShortSerial)
            .Select(display => new DisplaySnapshot(
                display.Id,
                display.ShortSerial,
                display.LongSerial,
                display.LocationNodeId,
                display.DeviceDefinitionId,
                display.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return results;
    }
}
