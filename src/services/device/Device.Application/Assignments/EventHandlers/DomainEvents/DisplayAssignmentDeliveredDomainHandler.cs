// <copyright file="DisplayAssignmentDeliveredDomainHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DisplayAssignmentAggregate.Events;
using Finbuckle.MultiTenant.Abstractions;
using SharedKernel.Events;
using SharedKernel.Infrastructure.MultiTenant;
using Wolverine;

namespace Device.Application.Assignments.EventHandlers.DomainEvents;

/// <summary>
/// Handles <see cref="DisplayAssignmentDeliveredEvent"/> by publishing a terminal-success integration event.
/// </summary>
public sealed partial class DisplayAssignmentDeliveredDomainHandler
{
    private const string DefaultEslProvider = "Stub";

    private readonly IMessageBus messageBus;
    private readonly IMultiTenantContextAccessor<TenantDetails>? tenantContextAccessor;
    private readonly ILogger<DisplayAssignmentDeliveredDomainHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayAssignmentDeliveredDomainHandler"/> class.
    /// </summary>
    /// <param name="messageBus">The Wolverine message bus.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="tenantContextAccessor">The current tenant context accessor.</param>
    public DisplayAssignmentDeliveredDomainHandler(
        IMessageBus messageBus,
        ILogger<DisplayAssignmentDeliveredDomainHandler> logger,
        IMultiTenantContextAccessor<TenantDetails>? tenantContextAccessor = null)
    {
        this.messageBus = messageBus;
        this.logger = logger;
        this.tenantContextAccessor = tenantContextAccessor;
    }

    /// <summary>
    /// Handles the assignment-delivered domain event.
    /// </summary>
    /// <param name="domainEvent">The domain event.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(DisplayAssignmentDeliveredEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        LogAssignmentDelivered(this.logger, domainEvent.AssignmentId, domainEvent.DisplayId);

        DisplayAssignmentDeliveredIntegrationEvent integrationEvent = new(
            domainEvent.AssignmentId,
            domainEvent.DisplayId,
            DefaultEslProvider);

        await PublishWithTenantAsync(integrationEvent).ConfigureAwait(false);
    }

    private async Task PublishWithTenantAsync(object integrationEvent)
    {
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

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "DisplayAssignment {AssignmentId} delivered to display {DisplayId}, publishing integration event")]
    private static partial void LogAssignmentDelivered(ILogger logger, Guid assignmentId, Guid displayId);
}
