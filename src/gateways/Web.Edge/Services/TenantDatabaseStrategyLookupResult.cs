// <copyright file="TenantDatabaseStrategyLookupResult.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Web.Edge.Services;

/// <summary>
/// Represents a tenant database strategy lookup outcome.
/// </summary>
/// <param name="Success">Indicates whether the lookup succeeded.</param>
/// <param name="DatabaseStrategy">The resolved database strategy.</param>
/// <param name="StatusCode">The HTTP status code to map on failure.</param>
/// <param name="ErrorCode">The machine-readable error code on failure.</param>
/// <param name="ErrorDetail">The human-readable error detail on failure.</param>
public sealed record TenantDatabaseStrategyLookupResult(
    bool Success,
    string? DatabaseStrategy,
    int? StatusCode,
    string? ErrorCode,
    string? ErrorDetail);
