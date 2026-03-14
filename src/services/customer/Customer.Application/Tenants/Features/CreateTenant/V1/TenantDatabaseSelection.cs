// <copyright file="TenantDatabaseSelection.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Pricing;

namespace Customer.Application.Tenants.Features.CreateTenant.V1;

/// <summary>
/// Database selection details for tenant creation.
/// </summary>
public sealed class TenantDatabaseSelection
{
    /// <summary>
    /// Gets the database strategy.
    /// </summary>
    public DatabaseStrategy DatabaseStrategy { get; init; } = default!;

    /// <summary>
    /// Gets the database provider.
    /// </summary>
    public DatabaseProvider DatabaseProvider { get; init; } = default!;
}
