// <copyright file="TenantConnectionSeedRpcItem.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace SharedKernel.Grpc.Contracts.Remote.V1.Tenants;

/// <summary>
/// Represents tenant seed data used to bootstrap message persistence mappings.
/// </summary>
public sealed class TenantConnectionSeedRpcItem
{
    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant slug/identifier.
    /// </summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant database strategy.
    /// </summary>
    public string DatabaseStrategy { get; set; } = string.Empty;
}
