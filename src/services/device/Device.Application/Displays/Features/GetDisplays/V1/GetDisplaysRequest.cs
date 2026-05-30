// <copyright file="GetDisplaysRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.Displays.Features.GetDisplays.V1;

/// <summary>
/// Request for listing displays by location node.
/// </summary>
public sealed class GetDisplaysRequest
{
    /// <summary>
    /// Gets or sets the location node identifier to filter displays by.
    /// </summary>
    public string LocationNodeId { get; set; } = string.Empty;
}
