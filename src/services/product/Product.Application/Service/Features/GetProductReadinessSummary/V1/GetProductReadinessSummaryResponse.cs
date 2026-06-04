// <copyright file="GetProductReadinessSummaryResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Product.Application.Service.Features.GetProductReadinessSummary.V1;

/// <summary>
/// Represents product readiness summary data.
/// </summary>
public sealed record GetProductReadinessSummaryResponse
{
    /// <summary>
    /// Gets the total number of products.
    /// </summary>
    public int ProductCount { get; init; }

    /// <summary>
    /// Gets the number of render-ready products.
    /// </summary>
    public int RenderReadyProductCount { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the summary was generated.
    /// </summary>
    public DateTimeOffset GeneratedAtUtc { get; init; }
}
