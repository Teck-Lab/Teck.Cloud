// <copyright file="GetLocationTemplateContextResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Features.GetLocationTemplateContext.V1;

public sealed record GetLocationTemplateContextResponse
{
    public string LocationNodeId { get; init; } = string.Empty;

    public string? ResolvedTemplateId { get; init; }

    public string TemplateSource { get; init; } = "None";

    public int AncestorDepthScanned { get; init; }
}
