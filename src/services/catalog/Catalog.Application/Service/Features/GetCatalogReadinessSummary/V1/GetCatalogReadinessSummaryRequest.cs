// <copyright file="GetCatalogReadinessSummaryRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Catalog.Application.Service.Features.GetCatalogReadinessSummary.V1;

/// <summary>
/// Request for catalog readiness summary.
/// </summary>
public sealed record GetCatalogReadinessSummaryRequest
{
    /// <summary>
    /// Gets a value indicating whether optional diagnostics should be included.
    /// </summary>
    public bool IncludeDiagnostics { get; init; }
}
