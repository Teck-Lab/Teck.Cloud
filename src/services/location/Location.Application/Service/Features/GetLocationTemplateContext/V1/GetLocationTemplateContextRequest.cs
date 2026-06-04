// <copyright file="GetLocationTemplateContextRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Features.GetLocationTemplateContext.V1;

/// <summary>
/// Request payload for retrieving template context by location node.
/// </summary>
public sealed record GetLocationTemplateContextRequest
{
    /// <summary>
    /// Gets the location node identifier.
    /// </summary>
    public string LocationNodeId { get; init; } = string.Empty;
}
