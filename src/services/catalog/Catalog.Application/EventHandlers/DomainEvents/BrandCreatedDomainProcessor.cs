using Catalog.Application.Brands.Features.CreateBrand.V1;
using Catalog.Domain.Entities.BrandAggregate;
using Catalog.Domain.Entities.BrandAggregate.Events;
using Wolverine.Persistence;

namespace Catalog.Application.EventHandlers.DomainEvents;

/// <summary>
/// Bridges brand domain events to integration events through Wolverine cascading messages.
/// </summary>
public static class BrandCreatedDomainEventProcessor
{
    /// <summary>
    /// Handles persisted brand domain events using Wolverine transactional storage primitives.
    /// </summary>
    /// <param name="command">The originating create brand command.</param>
    /// <param name="item">The brand aggregate tracked by Wolverine.</param>
    /// <param name="domainEvent">The domain event to propagate.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>A Wolverine storage action representing the pending update.</returns>
    public static IStorageAction<Brand> Handle(
        CreateBrandCommand command,
        [Entity] Brand item,
        [Entity] BrandCreatedDomainEvent domainEvent,
        ILogger logger)
    {
        logger.LogInformation("Brand {BrandId} created, emitting integration event", domainEvent.BrandId);
        item.AddDomainEvent(domainEvent);
        return Storage.Update(item);
    }
}
