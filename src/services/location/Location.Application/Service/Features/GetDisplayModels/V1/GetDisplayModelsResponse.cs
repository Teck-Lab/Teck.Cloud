// <copyright file="GetDisplayModelsResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Features.GetDisplayModels.V1;

/// <summary>
/// Response payload for display model list retrieval.
/// </summary>
public sealed record GetDisplayModelsResponse
{
    /// <summary>
    /// Gets the display models.
    /// </summary>
    public IReadOnlyList<GetDisplayModelItemResponse> DisplayModels { get; init; } = [];
}

/// <summary>
/// Response item for a display model.
/// </summary>
public sealed record GetDisplayModelItemResponse
{
    /// <summary>Gets the display model identifier.</summary>
    public string DisplayModelId { get; init; } = string.Empty;

    /// <summary>Gets the display model name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the display width.</summary>
    public int Width { get; init; }

    /// <summary>Gets the display height.</summary>
    public int Height { get; init; }
}
