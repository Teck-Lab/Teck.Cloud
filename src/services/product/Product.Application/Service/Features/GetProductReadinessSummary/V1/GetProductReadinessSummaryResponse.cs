// <copyright file="GetProductReadinessSummaryResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Product.Application.Service.Features.GetProductReadinessSummary.V1;

public sealed record GetProductReadinessSummaryResponse
{
    public int ProductCount { get; init; }

    public int RenderReadyProductCount { get; init; }

    public DateTimeOffset GeneratedAtUtc { get; init; }
}
