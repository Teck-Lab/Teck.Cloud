// <copyright file="IDeviceLayoutWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DeviceLayoutAggregate;

namespace Device.Application.DeviceLayouts.Abstractions;

/// <summary>
/// Write repository for <see cref="DeviceLayout"/> aggregate roots.
/// </summary>
public interface IDeviceLayoutWriteRepository
{
    /// <summary>
    /// Adds a new device layout.
    /// </summary>
    /// <param name="deviceLayout">The device layout to persist.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(DeviceLayout deviceLayout, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a device layout by its primary key.
    /// </summary>
    /// <param name="id">The device layout identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The device layout if found, otherwise null.</returns>
    Task<DeviceLayout?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
