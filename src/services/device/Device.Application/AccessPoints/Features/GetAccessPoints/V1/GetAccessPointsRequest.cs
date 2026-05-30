// <copyright file="GetAccessPointsRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.AccessPoints.Features.GetAccessPoints.V1;

/// <summary>
/// Request for listing access points by location node.
/// </summary>
public sealed class GetAccessPointsRequest
{
    /// <summary>
    /// Gets or sets the location node identifier to filter access points by.
    /// </summary>
    public string LocationNodeId { get; set; } = string.Empty;
}
