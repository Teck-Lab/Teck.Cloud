// <copyright file="CreateLocationNodeRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Features.CreateLocationNode.V1;

/// <summary>
/// Request payload for creating a location node.
/// </summary>
public sealed record CreateLocationNodeRequest
{
    /// <summary>
    /// Gets the location node name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the parent location node identifier.
    /// </summary>
    public string? ParentLocationNodeId { get; init; }
}
