// <copyright file="GetResolvedTemplateRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Features.GetResolvedTemplate.V1;

public sealed record GetResolvedTemplateRequest
{
    public string LocationNodeId { get; init; } = string.Empty;

    public string? ExplicitTemplateId { get; init; }
}
