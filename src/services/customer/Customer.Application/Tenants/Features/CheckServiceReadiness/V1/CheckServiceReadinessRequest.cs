// <copyright file="CheckServiceReadinessRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Application.Tenants.Features.CheckServiceReadiness.V1;

/// <summary>
/// Request to check if a service is ready for a tenant.
/// </summary>
public sealed class CheckServiceReadinessRequest
{
    /// <summary>
    /// Gets the tenant id.
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// Gets the service name.
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;
}
