// <copyright file="LicenseExpiredDomainHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Domain.Entities.LicenseAggregate.Events;
using SharedKernel.Events;
using Wolverine;

namespace Customer.Application.Licenses.EventHandlers.DomainEvents;

/// <summary>
/// Handles license-expired domain events by publishing integration events.
/// </summary>
public class LicenseExpiredDomainHandler
{
    private readonly IMessageBus messageBus;

    /// <summary>
    /// Initializes a new instance of the <see cref="LicenseExpiredDomainHandler"/> class.
    /// </summary>
    /// <param name="messageBus">The Wolverine message bus.</param>
    public LicenseExpiredDomainHandler(IMessageBus messageBus)
    {
        this.messageBus = messageBus;
    }

    /// <summary>
    /// Handles the license-expired domain event.
    /// </summary>
    /// <param name="domainEvent">The domain event.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(LicenseExpiredDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var integrationEvent = new TenantLicenseChangedIntegrationEvent(
            Guid.Parse(domainEvent.TenantId),
            domainEvent.LicenseId,
            "Active",
            "Expired",
            string.Empty);

        await this.messageBus.PublishAsync(integrationEvent).ConfigureAwait(false);
    }
}
