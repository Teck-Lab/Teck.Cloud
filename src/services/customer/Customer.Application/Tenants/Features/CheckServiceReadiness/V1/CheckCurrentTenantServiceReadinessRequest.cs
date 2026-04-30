// <copyright file="CheckCurrentTenantServiceReadinessRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Application.Tenants.Features.CheckServiceReadiness.V1;

/// <summary>
/// Request to check service readiness for the current tenant.
/// </summary>
public sealed class CheckCurrentTenantServiceReadinessRequest
{
    /// <summary>
    /// Gets the service name.
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;
}
