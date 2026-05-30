// <copyright file="DbDeviceLayoutWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceLayouts.Abstractions;
using Device.Domain.Entities.DeviceLayoutAggregate;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence.Database.EFCore;

namespace Device.Infrastructure.Persistence.Repositories.Write;

internal sealed class DbDeviceLayoutWriteRepository(
    DeviceWriteDbContext dbContext,
    IHttpContextAccessor httpContextAccessor)
    : GenericWriteRepository<DeviceLayout, Guid, DeviceWriteDbContext>(dbContext, httpContextAccessor),
      IDeviceLayoutWriteRepository
{
    /// <inheritdoc/>
    public new async Task AddAsync(DeviceLayout deviceLayout, CancellationToken cancellationToken)
    {
        await DbContext.DeviceLayouts.AddAsync(deviceLayout, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<DeviceLayout?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await DbContext.DeviceLayouts
            .FirstOrDefaultAsync(layout => layout.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }
}
