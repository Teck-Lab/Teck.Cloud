// <copyright file="GetDeviceItemResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.Devices.Features.GetDevices.V1;

/// <summary>
/// Response item for a single device definition.
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="ModelId">Unique supplier model code.</param>
/// <param name="Name">Human-readable model name.</param>
/// <param name="EslProvider">ESL vendor integration driver name.</param>
public sealed record GetDeviceItemResponse(
    Guid Id,
    string ModelId,
    string Name,
    string EslProvider);
