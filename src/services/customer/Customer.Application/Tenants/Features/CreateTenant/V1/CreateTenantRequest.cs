// <copyright file="CreateTenantRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Application.Tenants.Features.CreateTenant.V1;

/// <summary>
/// Request to create a new tenant.
/// </summary>
public sealed record CreateTenantRequest
{
    /// <summary>
    /// Gets the unique identifier for the tenant.
    /// </summary>
    public string Identifier { get; init; } = string.Empty;

    /// <summary>
    /// Gets the tenant name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the subscription plan.
    /// </summary>
    public string Plan { get; init; } = string.Empty;

    /// <summary>
    /// Gets the database strategy.
    /// </summary>
    public string DatabaseStrategy { get; init; } = string.Empty;
}
