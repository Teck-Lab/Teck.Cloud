// <copyright file="TenantCreatedEventDetails.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Customer.Domain.Entities.TenantAggregate.Events;

/// <summary>
/// Event details for <see cref="TenantCreatedDomainEvent"/>.
/// </summary>
public sealed class TenantCreatedEventDetails
{
    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// Gets the tenant slug/identifier.
    /// </summary>
    public string Identifier { get; init; } = string.Empty;

    /// <summary>
    /// Gets the tenant display name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the database strategy name.
    /// </summary>
    public string DatabaseStrategy { get; init; } = string.Empty;

    /// <summary>
    /// Gets the database provider name.
    /// </summary>
    public string DatabaseProvider { get; init; } = string.Empty;
}
