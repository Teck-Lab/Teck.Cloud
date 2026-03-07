// <copyright file="TenantCreatedDomainEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Events;

namespace Customer.Domain.Entities.TenantAggregate.Events;

/// <summary>
/// Domain event raised when a tenant is created.
/// </summary>
public sealed class TenantCreatedDomainEvent : DomainEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TenantCreatedDomainEvent"/> class.
    /// </summary>
    /// <param name="details">Tenant creation event details.</param>
    public TenantCreatedDomainEvent(TenantCreatedEventDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        this.TenantId = details.TenantId;
        this.Identifier = details.Identifier;
        this.Name = details.Name;
        this.DatabaseStrategy = details.DatabaseStrategy;
        this.DatabaseProvider = details.DatabaseProvider;
    }

    /// <summary>
    /// Gets the tenant ID.
    /// </summary>
    public Guid TenantId { get; }

    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public string Identifier { get; }

    /// <summary>
    /// Gets the tenant name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the database strategy.
    /// </summary>
    public string DatabaseStrategy { get; }

    /// <summary>
    /// Gets the database provider.
    /// </summary>
    public string DatabaseProvider { get; }
}
