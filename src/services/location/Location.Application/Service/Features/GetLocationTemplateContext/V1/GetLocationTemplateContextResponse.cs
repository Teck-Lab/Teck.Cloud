// <copyright file="GetLocationTemplateContextResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Features.GetLocationTemplateContext.V1;

/// <summary>
/// Response payload for resolved location template context.
/// </summary>
public sealed record GetLocationTemplateContextResponse
{
    /// <summary>
    /// Gets the location node identifier.
    /// </summary>
    public string LocationNodeId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the resolved template identifier.
    /// </summary>
    public string? ResolvedTemplateId { get; init; }

    /// <summary>
    /// Gets the source that provided the resolved template.
    /// </summary>
    public string TemplateSource { get; init; } = "None";

    /// <summary>
    /// Gets the number of ancestor levels scanned during resolution.
    /// </summary>
    public int AncestorDepthScanned { get; init; }
}
