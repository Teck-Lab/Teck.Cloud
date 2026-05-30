// <copyright file="DbAccessPointWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Http;
using SharedKernel.Persistence.Database.EFCore;

namespace Device.Infrastructure.Persistence.Repositories.Write;

internal sealed class DbAccessPointWriteRepository(
    DeviceWriteDbContext dbContext,
    IHttpContextAccessor httpContextAccessor)
    : GenericWriteRepository<Device.Domain.AccessPoints.AccessPoint, Guid, DeviceWriteDbContext>(dbContext, httpContextAccessor),
      Device.Domain.AccessPoints.IAccessPointWriteRepository
{
    /// <inheritdoc/>
    public new async Task AddAsync(Device.Domain.AccessPoints.AccessPoint accessPoint, CancellationToken ct)
    {
        await DbContext.AccessPoints.AddAsync(accessPoint, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(Device.Domain.AccessPoints.AccessPoint accessPoint, CancellationToken ct)
    {
        _ = ct;
        DbContext.AccessPoints.Update(accessPoint);
        return Task.CompletedTask;
    }
}
