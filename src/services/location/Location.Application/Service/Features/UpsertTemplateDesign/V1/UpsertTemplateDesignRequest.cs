// <copyright file="UpsertTemplateDesignRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Features.UpsertTemplateDesign.V1;

public sealed record UpsertTemplateDesignRequest
{
    public string TemplateId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int Width { get; init; }

    public int Height { get; init; }

    public string BackgroundColor { get; init; } = string.Empty;

    public string ElementsJson { get; init; } = string.Empty;

    public string DefaultsJson { get; init; } = "{}";
}
