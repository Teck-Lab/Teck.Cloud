// <copyright file="ServiceReadinessResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Api.Endpoints.V1.Tenants.CheckServiceReadiness;

/// <summary>
/// Response for service readiness check.
/// </summary>
internal sealed record ServiceReadinessResponse
{
    /// <summary>
    /// Gets a value indicating whether the service is ready.
    /// </summary>
    public bool Ready { get; init; }
}
