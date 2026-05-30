// <copyright file="UpdateAccessPointStatusRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.AccessPoints.Features.UpdateAccessPointStatus.V1;

/// <summary>
/// Request to update an access point status.
/// </summary>
public sealed class UpdateAccessPointStatusRequest
{
    /// <summary>
    /// Gets or sets the serial number from the route.
    /// </summary>
    public string Serial { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the new operational status.
    /// </summary>
    public string Status { get; set; } = string.Empty;
}
