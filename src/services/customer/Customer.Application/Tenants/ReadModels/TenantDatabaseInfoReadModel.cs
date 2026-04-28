// <copyright file="TenantDatabaseInfoReadModel.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Application.Tenants.ReadModels;

/// <summary>
/// Read model for tenant database metadata used by gRPC and migration consumers.
/// </summary>
public sealed class TenantDatabaseInfoReadModel
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
}
