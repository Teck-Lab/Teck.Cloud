// <copyright file="GetDeviceDefinitionByIdRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.DeviceDefinitions.Features.GetDeviceDefinitionById.V1;

/// <summary>
/// Route request for a single device definition by ID.
/// </summary>
public sealed class GetDeviceDefinitionByIdRequest
{
    /// <summary>Gets or sets the device definition identifier.</summary>
    public Guid Id { get; set; }
}
