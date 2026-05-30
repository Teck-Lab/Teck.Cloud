// <copyright file="GetDeviceLayoutsResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.DeviceLayouts.Features.GetDeviceLayouts.V1;

/// <summary>
/// A single device layout item in a list response.
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="DeviceDefinitionId">The device definition this layout belongs to.</param>
/// <param name="Name">Human-readable layout name.</param>
/// <param name="MaxZoneCount">Maximum number of content zones.</param>
public sealed record GetDeviceLayoutItemResponse(
    Guid Id,
    Guid DeviceDefinitionId,
    string Name,
    int MaxZoneCount);
