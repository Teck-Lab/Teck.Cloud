// <copyright file="DbAccessPointReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace Device.Infrastructure.Persistence.Repositories.Read;

public sealed class DbAccessPointReadRepository(DeviceReadDbContext dbContext) : Device.Domain.AccessPoints.IAccessPointReadRepository
{
    private readonly DeviceReadDbContext dbContext = dbContext;

    /// <inheritdoc/>
    public async Task<Device.Domain.AccessPoints.AccessPoint?> GetBySerialAsync(string serialNumber, CancellationToken ct)
    {
        return await this.dbContext.AccessPoints
            .AsNoTracking()
            .SingleOrDefaultAsync(accessPoint => accessPoint.SerialNumber == serialNumber, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Device.Domain.AccessPoints.AccessPoint>> GetByLocationAsync(
        string locationNodeId,
        CancellationToken ct)
    {
        List<Device.Domain.AccessPoints.AccessPoint> results = await this.dbContext.AccessPoints
            .AsNoTracking()
            .Where(accessPoint => accessPoint.LocationNodeId == locationNodeId)
            .OrderBy(accessPoint => accessPoint.SerialNumber)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return results;
    }

    /// <inheritdoc/>
    public async Task<Device.Domain.AccessPoints.AccessPoint?> FindByVendorAndLocationAsync(
        string vendor,
        string locationNodeId,
        CancellationToken ct)
    {
        return await this.dbContext.AccessPoints
            .AsNoTracking()
            .Where(accessPoint => accessPoint.Vendor == vendor && accessPoint.LocationNodeId == locationNodeId)
            .OrderBy(accessPoint => accessPoint.CurrentLoad)
            .ThenBy(accessPoint => accessPoint.SerialNumber)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }
}
