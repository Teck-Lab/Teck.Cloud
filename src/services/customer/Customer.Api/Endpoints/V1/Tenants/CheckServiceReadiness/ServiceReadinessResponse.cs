// <copyright file="ServiceReadinessResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable CA1515

namespace Customer.Api.Endpoints.V1.Tenants.CheckServiceReadiness;

/// <summary>
/// Response for service readiness check.
/// </summary>
public sealed record ServiceReadinessResponse
{
    /// <summary>
    /// Gets a value indicating whether the service is ready.
    /// </summary>
    public bool Ready { get; init; }
}
