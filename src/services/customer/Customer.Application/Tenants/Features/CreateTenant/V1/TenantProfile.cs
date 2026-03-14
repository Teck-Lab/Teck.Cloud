// <copyright file="TenantProfile.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Application.Tenants.Features.CreateTenant.V1;

/// <summary>
/// Tenant profile details for creation.
/// </summary>
public sealed class TenantProfile
{
    /// <summary>
    /// Gets the tenant display name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the tenant plan.
    /// </summary>
    public string Plan { get; init; } = string.Empty;
}
