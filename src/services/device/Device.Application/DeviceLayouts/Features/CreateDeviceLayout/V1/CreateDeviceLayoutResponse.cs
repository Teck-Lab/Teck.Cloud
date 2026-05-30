// <copyright file="CreateDeviceLayoutResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.DeviceLayouts.Features.CreateDeviceLayout.V1;

/// <summary>
/// Response returned after a device layout is created.
/// </summary>
/// <param name="Id">The identifier of the newly created device layout.</param>
public sealed record CreateDeviceLayoutResponse(Guid Id);
