// <copyright file="GetLocationTemplateContextRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Features.GetLocationTemplateContext.V1;

public sealed record GetLocationTemplateContextRequest
{
    public string LocationNodeId { get; init; } = string.Empty;
}
