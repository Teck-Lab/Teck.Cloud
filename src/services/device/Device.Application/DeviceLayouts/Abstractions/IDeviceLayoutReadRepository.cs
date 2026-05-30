// <copyright file="IDeviceLayoutReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Pagination;

namespace Device.Application.DeviceLayouts.Abstractions;

/// <summary>
/// Read repository for device layouts.
/// </summary>
public interface IDeviceLayoutReadRepository
{
    /// <summary>
    /// Gets all layouts for a given device definition, ordered by name.
    /// </summary>
    /// <param name="deviceDefinitionId">The device definition identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of layout snapshots for the specified definition.</returns>
    Task<IReadOnlyList<DeviceLayoutSnapshot>> GetByDeviceDefinitionIdAsync(Guid deviceDefinitionId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a paginated list of device layouts.
    /// </summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="size">Page size.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A paged list of device layout snapshots.</returns>
    Task<PagedList<DeviceLayoutSnapshot>> GetPagedAsync(int page, int size, CancellationToken cancellationToken);
}

/// <summary>
/// Snapshot of a device layout used across read queries.
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="DeviceDefinitionId">The device definition this layout belongs to.</param>
/// <param name="Name">Human-readable layout name.</param>
/// <param name="MaxZoneCount">Maximum number of content zones.</param>
public sealed record DeviceLayoutSnapshot(
    Guid Id,
    Guid DeviceDefinitionId,
    string Name,
    int MaxZoneCount);
