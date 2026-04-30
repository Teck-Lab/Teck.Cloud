// <copyright file="GetCurrentTenantDatabaseInfoResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Application.Tenants.Features.GetCurrentTenantDatabaseInfo.V1;

/// <summary>
/// Response for current tenant database metadata.
/// </summary>
public sealed class GetCurrentTenantDatabaseInfoResponse
{
    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Gets or sets the tenant slug/identifier.
    /// </summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant database strategy.
    /// </summary>
    public string DatabaseStrategy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant database provider.
    /// </summary>
    public string DatabaseProvider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the tenant has read replicas.
    /// </summary>
    public bool HasReadReplicas { get; set; }

    /// <summary>
    /// Gets or sets the optional service name used for replica evaluation.
    /// </summary>
    public string? ServiceName { get; set; }
}
