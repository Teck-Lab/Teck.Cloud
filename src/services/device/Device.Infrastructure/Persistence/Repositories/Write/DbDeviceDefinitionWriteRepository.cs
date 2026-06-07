// <copyright file="DbDeviceDefinitionWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceDefinitions.Abstractions;
using Device.Domain.Entities.DeviceDefinitionAggregate;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence.Database.EFCore;

namespace Device.Infrastructure.Persistence.Repositories.Write;

public sealed class DbDeviceDefinitionWriteRepository(
    DeviceWriteDbContext dbContext,
    IHttpContextAccessor httpContextAccessor)
    : GenericWriteRepository<DeviceDefinition, Guid, DeviceWriteDbContext>(dbContext, httpContextAccessor),
      IDeviceDefinitionWriteRepository
{
    /// <inheritdoc/>
    public new async Task AddAsync(DeviceDefinition deviceDefinition, CancellationToken cancellationToken)
    {
        await DbContext.DeviceDefinitions.AddAsync(deviceDefinition, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<DeviceDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await DbContext.DeviceDefinitions
            .FirstOrDefaultAsync(definition => definition.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsWithModelIdAsync(string modelId, CancellationToken cancellationToken)
    {
        return await DbContext.DeviceDefinitions
            .AsNoTracking()
            .AnyAsync(definition => definition.ModelId == modelId, cancellationToken)
            .ConfigureAwait(false);
    }
}
