// <copyright file="GetDeviceLayoutsRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.DeviceLayouts.Features.GetDeviceLayouts.V1;

/// <summary>
/// Request model for retrieving layouts for a specific device definition.
/// </summary>
public sealed class GetDeviceLayoutsRequest
{
    /// <summary>
    /// Gets or sets the device definition identifier to retrieve layouts for.
    /// </summary>
    public Guid DeviceDefinitionId { get; set; }
}
