// <copyright file="BrandCreatedDomainHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Catalog.Domain.Entities.BrandAggregate.Events;
using Finbuckle.MultiTenant.Abstractions;
using SharedKernel.Events;
using SharedKernel.Infrastructure.MultiTenant;
using Wolverine;

namespace Catalog.Application.Brands.EventHandlers.DomainEvents;

/// <summary>
/// Handles brand-created domain events by publishing integration events.
/// </summary>
public sealed partial class BrandCreatedDomainHandler
{
    private readonly IMessageBus messageBus;
    private readonly IMultiTenantContextAccessor<TenantDetails>? tenantContextAccessor;
    private readonly ILogger<BrandCreatedDomainHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrandCreatedDomainHandler"/> class.
    /// </summary>
    /// <param name="messageBus">The Wolverine message bus.</param>
    /// <param name="tenantContextAccessor">The current tenant context accessor.</param>
    /// <param name="logger">The logger instance.</param>
    public BrandCreatedDomainHandler(
        IMessageBus messageBus,
        ILogger<BrandCreatedDomainHandler> logger,
        IMultiTenantContextAccessor<TenantDetails>? tenantContextAccessor = null)
    {
        this.messageBus = messageBus;
        this.logger = logger;
        this.tenantContextAccessor = tenantContextAccessor;
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
        string? tenantId = ResolveTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            await this.messageBus.PublishAsync(integrationEvent).ConfigureAwait(false);
            return;
        }

        DeliveryOptions options = new()
        {
            TenantId = tenantId,
        };

        await this.messageBus.PublishAsync(integrationEvent, options).ConfigureAwait(false);
    }

    private string? ResolveTenantId()
    {
        if (this.messageBus is IMessageContext messageContext && !string.IsNullOrWhiteSpace(messageContext.TenantId))
        {
            return messageContext.TenantId;
        }

        return this.tenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Brand {BrandId} created, publishing integration event")]
    private static partial void LogBrandCreated(ILogger logger, Guid brandId);
}
