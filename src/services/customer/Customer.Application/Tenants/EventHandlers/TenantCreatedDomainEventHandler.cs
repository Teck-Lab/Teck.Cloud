using Customer.Domain.Entities.TenantAggregate.Events;
using SharedKernel.Events;
using Wolverine;

namespace Customer.Application.Tenants.EventHandlers;

/// <summary>
/// Handler for TenantCreatedDomainEvent.
/// Publishes a TenantCreatedIntegrationEvent to Wolverine/RabbitMQ for downstream services.
/// </summary>
public class TenantCreatedHandler
{
    private readonly IMessageBus _messageBus;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantCreatedHandler"/> class.
    /// </summary>
    /// <param name="messageBus">The Wolverine message bus.</param>
    public TenantCreatedHandler(IMessageBus messageBus)
    {
        _messageBus = messageBus;
    }

    /// <summary>
    /// Handles the TenantCreatedDomainEvent by publishing an integration event.
    /// This method is automatically invoked by Wolverine when the domain event is raised.
    /// </summary>
    /// <param name="domainEvent">The domain event.</param>
    /// <returns>Task representing the async operation.</returns>
    public async Task Handle(TenantCreatedDomainEvent domainEvent)
    {
        // Create integration event
        var integrationEvent = new TenantCreatedIntegrationEvent(
            domainEvent.TenantId,
            domainEvent.Identifier,
            domainEvent.Name,
            domainEvent.DatabaseStrategy,
            domainEvent.DatabaseProvider);

        // Publish to RabbitMQ via Wolverine
        await _messageBus.PublishAsync(integrationEvent);
    }
}
