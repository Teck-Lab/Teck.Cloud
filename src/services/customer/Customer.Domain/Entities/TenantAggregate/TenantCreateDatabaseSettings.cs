// <copyright file="TenantCreateDatabaseSettings.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Pricing;

namespace Customer.Domain.Entities.TenantAggregate;

/// <summary>
/// Database settings used during tenant creation.
/// </summary>
public sealed class TenantCreateDatabaseSettings
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
