// <copyright file="DbDisplayWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Displays.Abstractions;
using Device.Domain.Entities.DisplayAggregate;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence.Database.EFCore;

namespace Device.Infrastructure.Persistence.Repositories.Write;

internal sealed class DbDisplayWriteRepository(
    DeviceWriteDbContext dbContext,
    IHttpContextAccessor httpContextAccessor)
    : GenericWriteRepository<Display, Guid, DeviceWriteDbContext>(dbContext, httpContextAccessor),
      IDisplayWriteRepository
{
    /// <inheritdoc/>
    public async Task<bool> ExistsWithShortSerialGlobalAsync(
        string shortSerial,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.Displays
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(display => display.ShortSerial == shortSerial, cancellationToken)
            .ConfigureAwait(false);
    }
}
