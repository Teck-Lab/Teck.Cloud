// <copyright file="IDeviceDefinitionWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DeviceDefinitionAggregate;

namespace Device.Application.DeviceDefinitions.Abstractions;

/// <summary>
/// Write repository for <see cref="DeviceDefinition"/> aggregate roots.
/// </summary>
public interface IDeviceDefinitionWriteRepository
{
    /// <summary>
    /// Adds a new device definition.
    /// </summary>
    /// <param name="deviceDefinition">The device definition to persist.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(DeviceDefinition deviceDefinition, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a device definition by its primary key.
    /// </summary>
    /// <param name="id">The device definition identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The device definition if found, otherwise null.</returns>
    Task<DeviceDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Checks whether a model ID is already registered.
    /// </summary>
    /// <param name="modelId">The model identifier to check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if a definition with this model ID already exists.</returns>
    Task<bool> ExistsWithModelIdAsync(string modelId, CancellationToken cancellationToken);
}
