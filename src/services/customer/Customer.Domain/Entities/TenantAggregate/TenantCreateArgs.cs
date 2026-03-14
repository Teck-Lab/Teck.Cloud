// <copyright file="TenantCreateArgs.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Domain.Entities.TenantAggregate;

/// <summary>
/// Arguments required to create a tenant.
/// </summary>
public sealed class TenantCreateArgs
{
    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public string Identifier { get; init; } = string.Empty;

    /// <summary>
    /// Gets the tenant display name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the tenant plan.
    /// </summary>
    public string Plan { get; init; } = string.Empty;

    /// <summary>
    /// Gets the tenant database settings.
    /// </summary>
    public TenantCreateDatabaseSettings Database { get; init; } = new();
}
