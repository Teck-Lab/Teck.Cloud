// <copyright file="DisplayAssignmentRenderedDomainHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.AccessPoints;
using Device.Application.Assignments.Abstractions;
using Device.Application.Operations.Saga;
using Device.Domain.Entities.DisplayAssignmentAggregate.Events;
using ErrorOr;
using Finbuckle.MultiTenant.Abstractions;
using SharedKernel.Core.Database;
using SharedKernel.Events;
using SharedKernel.Infrastructure.MultiTenant;
using Wolverine;
using DomainAccessPointWriteRepository = Device.Domain.AccessPoints.IAccessPointWriteRepository;

namespace Device.Application.Assignments.EventHandlers.DomainEvents;

/// <summary>
/// Handles <see cref="DisplayAssignmentRenderedEvent"/> by publishing the corresponding integration event
/// so vendor device-server workers can dispatch the rendered image to the physical ESL.
/// </summary>
public sealed partial class DisplayAssignmentRenderedDomainHandler
{
    private readonly EffectiveAccessPointResolver accessPointResolver;
    private readonly DomainAccessPointWriteRepository accessPointWriteRepository;
    private readonly Device.Application.Assignments.Abstractions.IDisplayAssignmentReadRepository displayAssignmentReadRepository;
    private readonly IUnitOfWork unitOfWork;
    private readonly IMessageBus messageBus;
    private readonly IMultiTenantContextAccessor<TenantDetails>? tenantContextAccessor;
    private readonly ILogger<DisplayAssignmentRenderedDomainHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayAssignmentRenderedDomainHandler"/> class.
    /// </summary>
    /// <param name="accessPointResolver">The effective access point resolver.</param>
    /// <param name="accessPointWriteRepository">The access point write repository.</param>
    /// <param name="displayAssignmentReadRepository">The display assignment read repository.</param>
    /// <param name="unitOfWork">The unit of work used to persist access point load changes.</param>
    /// <param name="messageBus">The Wolverine message bus.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="tenantContextAccessor">The current tenant context accessor.</param>
    public DisplayAssignmentRenderedDomainHandler(
        EffectiveAccessPointResolver accessPointResolver,
        DomainAccessPointWriteRepository accessPointWriteRepository,
        Device.Application.Assignments.Abstractions.IDisplayAssignmentReadRepository displayAssignmentReadRepository,
        IUnitOfWork unitOfWork,
        IMessageBus messageBus,
        ILogger<DisplayAssignmentRenderedDomainHandler> logger,
        IMultiTenantContextAccessor<TenantDetails>? tenantContextAccessor = null)
    {
        this.accessPointResolver = accessPointResolver;
        this.accessPointWriteRepository = accessPointWriteRepository;
        this.displayAssignmentReadRepository = displayAssignmentReadRepository;
        this.unitOfWork = unitOfWork;
        this.messageBus = messageBus;
        this.logger = logger;
        this.tenantContextAccessor = tenantContextAccessor;
    }

    /// <summary>
    /// Handles the assignment-rendered domain event.
    /// </summary>
    /// <param name="domainEvent">The domain event.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(DisplayAssignmentRenderedEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        LogAssignmentRendered(this.logger, domainEvent.AssignmentId, domainEvent.DisplayId);

        EffectiveAccessPoint? resolved = await this.accessPointResolver
            .ResolveAsync(domainEvent.DisplayId, CancellationToken.None)
            .ConfigureAwait(false);

        if (resolved?.AccessPoint is null)
        {
            string provider = resolved?.Provider ?? string.Empty;
            string reason = string.IsNullOrWhiteSpace(provider)
                ? "No available ESL access point for display location"
                : $"No available {provider} AP for display location";

            await PublishWithTenantAsync(new EslDispatchFailedIntegrationEvent
            {
                AssignmentId = domainEvent.AssignmentId,
                DisplayId = domainEvent.DisplayId,
                EslProvider = provider,
                Reason = reason,
                FailedAt = DateTimeOffset.UtcNow,
            }).ConfigureAwait(false);

            LogAccessPointUnavailable(this.logger, domainEvent.AssignmentId, domainEvent.DisplayId, provider);
            return;
        }

        ErrorOr<Success> increment = resolved.AccessPoint.IncrementLoad();
        if (increment.IsError)
        {
            await PublishWithTenantAsync(new EslDispatchFailedIntegrationEvent
            {
                AssignmentId = domainEvent.AssignmentId,
                DisplayId = domainEvent.DisplayId,
                EslProvider = resolved.Provider,
                Reason = increment.FirstError.Description,
                FailedAt = DateTimeOffset.UtcNow,
            }).ConfigureAwait(false);

            LogAccessPointLoadRejected(this.logger, domainEvent.AssignmentId, resolved.AccessPoint.SerialNumber, increment.FirstError.Description);
            return;
        }

        await this.accessPointWriteRepository.UpdateAsync(resolved.AccessPoint, CancellationToken.None).ConfigureAwait(false);
        await this.unitOfWork.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

        await PublishWithTenantAsync(new SetDisplayOperationAccessPoint(
            domainEvent.DisplayId,
            resolved.AccessPoint.SerialNumber)).ConfigureAwait(false);

        await PublishWithTenantAsync(new AccessPointLoadChangedIntegrationEvent
        {
            AccessPointId = resolved.AccessPoint.Id,
            SerialNumber = resolved.AccessPoint.SerialNumber,
            LocationNodeId = resolved.AccessPoint.LocationNodeId,
            PreviousLoad = resolved.AccessPoint.CurrentLoad - 1,
            NewLoad = resolved.AccessPoint.CurrentLoad,
            MaxCapacity = resolved.AccessPoint.MaxCapacity,
            ChangedAt = DateTimeOffset.UtcNow,
        }).ConfigureAwait(false);

        DisplayAssignmentSummary? assignment = await this.displayAssignmentReadRepository
            .GetSummaryByIdAsync(domainEvent.AssignmentId, CancellationToken.None)
            .ConfigureAwait(false);

        DisplayAssignmentRenderedIntegrationEvent integrationEvent = new()
        {
            AssignmentId = domainEvent.AssignmentId,
            DisplayId = domainEvent.DisplayId,
            RenderJobId = assignment?.RenderJobId ?? Guid.Empty,
            RenderedImageUri = domainEvent.RenderedImageUri,
            EslProvider = resolved.Provider,
            AccessPointSerial = resolved.AccessPoint.SerialNumber,
        };

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

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "DisplayAssignment {AssignmentId} rendered for display {DisplayId}, publishing integration event")]
    private static partial void LogAssignmentRendered(ILogger logger, Guid assignmentId, Guid displayId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "No available access point for assignment {AssignmentId}, display {DisplayId}, provider {Provider}")]
    private static partial void LogAccessPointUnavailable(ILogger logger, Guid assignmentId, Guid displayId, string provider);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Access point load increment rejected for assignment {AssignmentId}, AP {AccessPointSerial}: {Reason}")]
    private static partial void LogAccessPointLoadRejected(ILogger logger, Guid assignmentId, string accessPointSerial, string reason);
}
