// <copyright file="IDeviceDefinitionReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.Assignments.Abstractions;

/// <summary>
/// Provides assignment-scoped layout context for a given display.
/// Resolves the zone capacity constraint needed during label assignment.
/// </summary>
public interface IDeviceDefinitionReadRepository
{
    /// <summary>
    /// Gets the layout context for a display.
    /// Returns null when the display does not have a layout assigned.
    /// </summary>
    /// <param name="displayId">The display identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The layout context if the display exists and has a layout assigned; otherwise null.</returns>
    ValueTask<DisplayLayoutContext?> GetLayoutContextByDisplayIdAsync(Guid displayId, CancellationToken cancellationToken);
}

/// <summary>
/// Layout context used during assignment to enforce zone count constraints.
/// </summary>
/// <param name="DisplayId">The display identifier.</param>
/// <param name="DeviceLayoutId">The layout identifier assigned to this display.</param>
/// <param name="MaxZoneCount">Maximum number of zones permitted by the assigned layout.</param>
public sealed record DisplayLayoutContext(Guid DisplayId, Guid DeviceLayoutId, int MaxZoneCount);
