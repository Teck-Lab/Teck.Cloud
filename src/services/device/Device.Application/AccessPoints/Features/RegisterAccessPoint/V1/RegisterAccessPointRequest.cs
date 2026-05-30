// <copyright file="RegisterAccessPointRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.AccessPoints.Features.RegisterAccessPoint.V1;

/// <summary>
/// Request to register an access point to a location node.
/// </summary>
public sealed class RegisterAccessPointRequest
{
    /// <summary>
    /// Gets or sets the supplier serial number.
    /// </summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the vendor name.
    /// </summary>
    public string Vendor { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the location node identifier.
    /// </summary>
    public string LocationNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum supported display count.
    /// </summary>
    public int MaxCapacity { get; set; }
}
