// <copyright file="UpsertLocationGroupRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Features.UpsertLocationGroup.V1;

/// <summary>
/// Request payload for creating or updating a location group.
/// </summary>
public sealed record UpsertLocationGroupRequest
{
    /// <summary>
    /// Gets the location group identifier.
    /// </summary>
    public string LocationGroupId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the location group name.
    /// </summary>
    public string Name { get; init; } = string.Empty;
}
