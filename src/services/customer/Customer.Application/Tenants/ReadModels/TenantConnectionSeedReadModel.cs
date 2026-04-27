// <copyright file="TenantConnectionSeedReadModel.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Application.Tenants.ReadModels;

/// <summary>
/// Represents tenant seed data required to bootstrap per-tenant message persistence mappings.
/// </summary>
public sealed class TenantConnectionSeedReadModel
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
}
