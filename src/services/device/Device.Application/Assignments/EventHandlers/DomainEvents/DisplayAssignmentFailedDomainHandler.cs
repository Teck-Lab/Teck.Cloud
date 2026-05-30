// <copyright file="DisplayAssignmentFailedDomainHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DisplayAssignmentAggregate.Events;
using Finbuckle.MultiTenant.Abstractions;
using SharedKernel.Events;
using SharedKernel.Infrastructure.MultiTenant;
using Wolverine;

namespace Device.Application.Assignments.EventHandlers.DomainEvents;

/// <summary>
/// Handles <see cref="DisplayAssignmentFailedEvent"/> by publishing a terminal-failure integration event.
/// </summary>
public sealed partial class DisplayAssignmentFailedDomainHandler
{
    private const string DefaultFailedStage = "Render";

    private readonly IMessageBus messageBus;
    private readonly IMultiTenantContextAccessor<TenantDetails>? tenantContextAccessor;
    private readonly ILogger<DisplayAssignmentFailedDomainHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayAssignmentFailedDomainHandler"/> class.
    /// </summary>
    /// <param name="messageBus">The Wolverine message bus.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="tenantContextAccessor">The current tenant context accessor.</param>
    public DisplayAssignmentFailedDomainHandler(
        IMessageBus messageBus,
        ILogger<DisplayAssignmentFailedDomainHandler> logger,
        IMultiTenantContextAccessor<TenantDetails>? tenantContextAccessor = null)
    {
        this.messageBus = messageBus;
        this.logger = logger;
        this.tenantContextAccessor = tenantContextAccessor;
    }

    /// <summary>
    /// Handles the assignment-failed domain event.
    /// </summary>
    /// <param name="domainEvent">The domain event.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(DisplayAssignmentFailedEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        LogAssignmentFailed(this.logger, domainEvent.AssignmentId, domainEvent.DisplayId, domainEvent.FailureReason);

        DisplayAssignmentFailedIntegrationEvent integrationEvent = new(
            domainEvent.AssignmentId,
            domainEvent.DisplayId,
            domainEvent.FailureReason,
            DefaultFailedStage);

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

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "DisplayAssignment {AssignmentId} failed for display {DisplayId}: {FailureReason}")]
    private static partial void LogAssignmentFailed(ILogger logger, Guid assignmentId, Guid displayId, string failureReason);
}
