// <copyright file="GetProductReadinessSummaryRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Product.Application.Service.Features.GetProductReadinessSummary.V1;

/// <summary>
/// Represents a request for obtaining product readiness summary data.
/// </summary>
public sealed record GetProductReadinessSummaryRequest
{
    /// <summary>
    /// Gets a value indicating whether diagnostic details are included in the summary.
    /// </summary>
    public bool IncludeDiagnostics { get; init; }
}
