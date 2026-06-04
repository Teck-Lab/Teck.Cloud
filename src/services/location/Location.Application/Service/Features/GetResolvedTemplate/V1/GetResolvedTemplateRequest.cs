// <copyright file="GetResolvedTemplateRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Features.GetResolvedTemplate.V1;

/// <summary>
/// Request payload for resolved template lookup.
/// </summary>
public sealed record GetResolvedTemplateRequest
{
    /// <summary>
    /// Gets the location node identifier.
    /// </summary>
    public string LocationNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the explicit template identifier.
    /// </summary>
    public string? ExplicitTemplateId { get; init; }
}
