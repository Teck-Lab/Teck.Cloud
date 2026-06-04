// <copyright file="UpsertTemplateDesignRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Features.UpsertTemplateDesign.V1;

/// <summary>
/// Request payload for creating or updating template design settings.
/// </summary>
public sealed record UpsertTemplateDesignRequest
{
    /// <summary>
    /// Gets the template identifier.
    /// </summary>
    public string TemplateId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the template name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the template width.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Gets the template height.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Gets the template background color.
    /// </summary>
    public string BackgroundColor { get; init; } = string.Empty;

    /// <summary>
    /// Gets the serialized elements payload.
    /// </summary>
    public string ElementsJson { get; init; } = string.Empty;

    /// <summary>
    /// Gets the serialized defaults payload.
    /// </summary>
    public string DefaultsJson { get; init; } = "{}";
}
