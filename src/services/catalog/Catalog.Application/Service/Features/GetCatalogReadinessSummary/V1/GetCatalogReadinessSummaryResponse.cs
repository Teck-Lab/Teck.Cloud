// <copyright file="GetCatalogReadinessSummaryResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Catalog.Application.Service.Features.GetCatalogReadinessSummary.V1;

/// <summary>
/// Readiness summary for catalog discovery footprint.
/// </summary>
public sealed class GetCatalogReadinessSummaryResponse
{
    /// <summary>
    /// Gets or sets total brand count.
    /// </summary>
    public int BrandCount { get; set; }

    /// <summary>
    /// Gets or sets total product count.
    /// </summary>
    public int ProductCount { get; set; }

    /// <summary>
    /// Gets or sets active promotion count.
    /// </summary>
    public int ActivePromotionCount { get; set; }

    /// <summary>
    /// Gets or sets generated timestamp in UTC.
    /// </summary>
    public DateTimeOffset GeneratedAtUtc { get; set; }
}
