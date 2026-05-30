// <copyright file="RegisterDeviceDefinitionResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.DeviceDefinitions.Features.RegisterDeviceDefinition.V1;

/// <summary>
/// Response returned after a device definition is registered.
/// </summary>
/// <param name="Id">The identifier of the newly registered device definition.</param>
public sealed record RegisterDeviceDefinitionResponse(Guid Id);
