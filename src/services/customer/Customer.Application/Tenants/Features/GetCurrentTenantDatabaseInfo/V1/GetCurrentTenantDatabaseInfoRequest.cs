// <copyright file="GetCurrentTenantDatabaseInfoRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Application.Tenants.Features.GetCurrentTenantDatabaseInfo.V1;

/// <summary>
/// Request model for current tenant database metadata.
/// </summary>
public sealed class GetCurrentTenantDatabaseInfoRequest
{
    /// <summary>
    /// Gets the optional service name used for read replica evaluation.
    /// </summary>
    public string? ServiceName { get; init; }
}
