// <copyright file="LicenseActivatedDomainHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Domain.Entities.LicenseAggregate.Events;
using SharedKernel.Events;
using Wolverine;

namespace Customer.Application.Licenses.EventHandlers.DomainEvents;

/// <summary>
/// Handles license-activated domain events by publishing integration events.
/// </summary>
public class LicenseActivatedDomainHandler
{
    private readonly IMessageBus messageBus;

    /// <summary>
    /// Initializes a new instance of the <see cref="LicenseActivatedDomainHandler"/> class.
    /// </summary>
    /// <param name="messageBus">The Wolverine message bus.</param>
    public LicenseActivatedDomainHandler(IMessageBus messageBus)
    {
        this.messageBus = messageBus;
    }

    /// <summary>
    /// Handles the license-activated domain event.
    /// </summary>
    /// <param name="domainEvent">The domain event.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(LicenseActivatedDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var integrationEvent = new TenantLicenseChangedIntegrationEvent(
            Guid.Parse(domainEvent.TenantId),
            domainEvent.LicenseId,
            "Trial",
            "Active",
            string.Empty);

        await this.messageBus.PublishAsync(integrationEvent).ConfigureAwait(false);
    }
}
