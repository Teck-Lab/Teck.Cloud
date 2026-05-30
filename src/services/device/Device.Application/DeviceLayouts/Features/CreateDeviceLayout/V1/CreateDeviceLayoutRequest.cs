// <copyright file="CreateDeviceLayoutRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.DeviceLayouts.Features.CreateDeviceLayout.V1;

/// <summary>
/// HTTP request body for creating a device layout.
/// </summary>
public sealed class CreateDeviceLayoutRequest
{
    /// <summary>Gets or sets the device definition this layout belongs to.</summary>
    public Guid DeviceDefinitionId { get; set; }

    /// <summary>Gets or sets the human-readable layout name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the maximum number of content zones.</summary>
    public int MaxZoneCount { get; set; }
}
