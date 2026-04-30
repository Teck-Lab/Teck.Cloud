// <copyright file="GetTenantDatabaseInfoRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Application.Tenants.Features.GetCurrentTenantDatabaseInfo.V1;

/// <summary>
/// Request to get tenant database info for a specific service.
/// </summary>
public sealed class GetTenantDatabaseInfoRequest
{
    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// Gets the optional service name used for read replica evaluation.
    /// </summary>
    public string? ServiceName { get; init; }
}
