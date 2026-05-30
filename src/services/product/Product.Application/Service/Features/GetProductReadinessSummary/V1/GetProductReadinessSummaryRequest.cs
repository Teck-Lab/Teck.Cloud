// <copyright file="GetProductReadinessSummaryRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Product.Application.Service.Features.GetProductReadinessSummary.V1;

public sealed record GetProductReadinessSummaryRequest
{
    public bool IncludeDiagnostics { get; init; }
}
