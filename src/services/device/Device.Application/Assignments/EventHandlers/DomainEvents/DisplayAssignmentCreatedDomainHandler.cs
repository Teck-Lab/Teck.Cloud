// <copyright file="DisplayAssignmentCreatedDomainHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.Abstractions;
using Device.Application.Operations.Saga;
using Device.Domain.Entities.DisplayAssignmentAggregate;
using Device.Domain.Entities.DisplayAssignmentAggregate.Events;
using Finbuckle.MultiTenant.Abstractions;
using SharedKernel.Events;
using SharedKernel.Infrastructure.MultiTenant;
using Wolverine;

namespace Device.Application.Assignments.EventHandlers.DomainEvents;

/// <summary>
/// Handles <see cref="DisplayAssignmentCreatedEvent"/> by publishing the corresponding integration event
/// so the image-generator can enqueue a render job for the new assignment.
/// </summary>
public sealed partial class DisplayAssignmentCreatedDomainHandler
{
    private const string AssignOperationType = "Assign";

    private readonly IMessageBus messageBus;
    private readonly IDisplayAssignmentWriteRepository displayAssignmentWriteRepository;
    private readonly IMultiTenantContextAccessor<TenantDetails>? tenantContextAccessor;
    private readonly ILogger<DisplayAssignmentCreatedDomainHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayAssignmentCreatedDomainHandler"/> class.
    /// </summary>
    /// <param name="messageBus">The Wolverine message bus.</param>
    /// <param name="displayAssignmentWriteRepository">The display assignment write repository.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="tenantContextAccessor">The current tenant context accessor.</param>
    public DisplayAssignmentCreatedDomainHandler(
        IMessageBus messageBus,
        IDisplayAssignmentWriteRepository displayAssignmentWriteRepository,
        ILogger<DisplayAssignmentCreatedDomainHandler> logger,
        IMultiTenantContextAccessor<TenantDetails>? tenantContextAccessor = null)
    {
        this.messageBus = messageBus;
        this.displayAssignmentWriteRepository = displayAssignmentWriteRepository;
        this.logger = logger;
        this.tenantContextAccessor = tenantContextAccessor;
    }

    /// <summary>
    /// Handles the assignment-created domain event.
    /// </summary>
    /// <param name="domainEvent">The domain event.</param>
    /// <param name="cancellationToken">The cancellation token propagated by the Wolverine runtime.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(DisplayAssignmentCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        LogAssignmentCreated(this.logger, domainEvent.AssignmentId, domainEvent.DisplayId, domainEvent.RenderJobId);

        DisplayAssignmentCreatedIntegrationEvent integrationEvent = new(
            domainEvent.AssignmentId,
            domainEvent.DisplayId,
            domainEvent.RenderJobId,
            assignmentVersion: 1);

        await PublishWithTenantAsync(integrationEvent).ConfigureAwait(false);

        DisplayAssignment? assignment = await this.displayAssignmentWriteRepository
            .FindOneAsync(
                assignment => assignment.Id == domainEvent.AssignmentId,
                enableTracking: false,
                cancellationToken)
            .ConfigureAwait(false);

        if (assignment is null)
        {
            LogAssignmentNotFoundForOperation(this.logger, domainEvent.AssignmentId, domainEvent.DisplayId);
            return;
        }

        string tenantId = ResolveTenantId() ?? string.Empty;
        StartDisplayOperation startOperation = new(
            domainEvent.DisplayId,
            assignment.LocationNodeId,
            tenantId,
            AssignOperationType,
            CreateAssignPayload(domainEvent.AssignmentId, domainEvent.RenderJobId),
            DateTimeOffset.UtcNow);

        await PublishWithTenantAsync(startOperation).ConfigureAwait(false);
    }

    private static string CreateAssignPayload(Guid assignmentId, Guid renderJobId)
    {
        return $$"""{"assignmentId":"{{assignmentId}}","renderJobId":"{{renderJobId}}"}""";
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

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "DisplayAssignment {AssignmentId} created for display {DisplayId}, render job {RenderJobId} queued")]
    private static partial void LogAssignmentCreated(ILogger logger, Guid assignmentId, Guid displayId, Guid renderJobId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "DisplayAssignment {AssignmentId} was not found while starting display operation saga for display {DisplayId}")]
    private static partial void LogAssignmentNotFoundForOperation(ILogger logger, Guid assignmentId, Guid displayId);
}
