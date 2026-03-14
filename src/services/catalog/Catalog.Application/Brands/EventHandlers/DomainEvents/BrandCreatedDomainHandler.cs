// <copyright file="BrandCreatedDomainHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Domain.Entities.BrandAggregate.Events;
using SharedKernel.Events;
using Wolverine;

namespace Catalog.Application.Brands.EventHandlers.DomainEvents;

/// <summary>
/// Handles brand-created domain events by publishing integration events.
/// </summary>
public sealed partial class BrandCreatedDomainHandler
{
    private readonly IMessageBus messageBus;
    private readonly ILogger<BrandCreatedDomainHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrandCreatedDomainHandler"/> class.
    /// </summary>
    /// <param name="messageBus">The Wolverine message bus.</param>
    /// <param name="logger">The logger instance.</param>
    public BrandCreatedDomainHandler(IMessageBus messageBus, ILogger<BrandCreatedDomainHandler> logger)
    {
        this.messageBus = messageBus;
        this.logger = logger;
    }

    /// <summary>
    /// Handles the brand-created domain event.
    /// </summary>
    /// <param name="domainEvent">The domain event.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(BrandCreatedDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        LogBrandCreated(this.logger, domainEvent.BrandId);

        BrandCreatedIntegrationEvent integrationEvent = new(domainEvent.BrandId);
        await this.messageBus.PublishAsync(integrationEvent).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Brand {BrandId} created, publishing integration event")]
    private static partial void LogBrandCreated(ILogger logger, Guid brandId);
}
