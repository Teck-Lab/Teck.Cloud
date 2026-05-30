// <copyright file="GetDisplayModelsResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Features.GetDisplayModels.V1;

public sealed record GetDisplayModelsResponse
{
    public IReadOnlyList<GetDisplayModelItemResponse> DisplayModels { get; init; } = [];
}

public sealed record GetDisplayModelItemResponse
{
    public string DisplayModelId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int Width { get; init; }

    public int Height { get; init; }
}