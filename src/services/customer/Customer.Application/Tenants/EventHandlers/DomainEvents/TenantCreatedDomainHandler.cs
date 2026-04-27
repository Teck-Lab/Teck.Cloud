// <copyright file="TenantCreatedDomainHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Domain.Entities.TenantAggregate.Events;
using SharedKernel.Events;
using Wolverine;

namespace Customer.Application.Tenants.EventHandlers.DomainEvents;

/// <summary>
/// Handles tenant-created domain events by publishing integration events.
/// </summary>
public class TenantCreatedDomainHandler
{
    private readonly IMessageBus messageBus;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantCreatedDomainHandler"/> class.
    /// </summary>
    /// <param name="messageBus">The Wolverine message bus.</param>
    public TenantCreatedDomainHandler(IMessageBus messageBus)
    {
        this.messageBus = messageBus;
    }

    /// <summary>
    /// Handles the tenant-created domain event.
    /// </summary>
    /// <param name="domainEvent">The domain event.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(TenantCreatedDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        TenantCreatedIntegrationEvent integrationEvent = new(
            domainEvent.TenantId,
            domainEvent.Identifier,
            domainEvent.Name,
            domainEvent.DatabaseStrategy,
            domainEvent.DatabaseProvider);

        DeliveryOptions options = new()
        {
            TenantId = domainEvent.TenantId.ToString("D"),
        };

        await this.messageBus.PublishAsync(integrationEvent, options).ConfigureAwait(false);
    }
}
