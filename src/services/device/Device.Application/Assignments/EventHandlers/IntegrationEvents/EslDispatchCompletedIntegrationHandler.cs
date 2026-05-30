// <copyright file="EslDispatchCompletedIntegrationHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.Abstractions;
using Device.Application.Operations.Saga;
using Device.Domain.AccessPoints;
using Device.Domain.Entities.DisplayAssignmentAggregate;
using ErrorOr;
using SharedKernel.Core.Database;
using SharedKernel.Events;
using Wolverine;

namespace Device.Application.Assignments.EventHandlers.IntegrationEvents;

/// <summary>
/// Consumes <see cref="EslDispatchCompletedIntegrationEvent"/> emitted by a vendor ESL worker after
/// a successful physical dispatch, and transitions the matching <see cref="DisplayAssignment"/>
/// from <c>Rendered</c> to <c>Delivered</c>.
/// </summary>
public sealed partial class EslDispatchCompletedIntegrationHandler
{
    private const string AssignOperationType = "Assign";

    private readonly IAccessPointReadRepository accessPointReadRepository;
    private readonly IAccessPointWriteRepository accessPointWriteRepository;
    private readonly IDisplayAssignmentWriteRepository displayAssignmentWriteRepository;
    private readonly IUnitOfWork unitOfWork;
    private readonly IMessageBus messageBus;
    private readonly ILogger<EslDispatchCompletedIntegrationHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EslDispatchCompletedIntegrationHandler"/> class.
    /// </summary>
    /// <param name="accessPointReadRepository">The access point read repository.</param>
    /// <param name="accessPointWriteRepository">The access point write repository.</param>
    /// <param name="displayAssignmentWriteRepository">The display assignment write repository.</param>
    /// <param name="unitOfWork">The unit of work used to persist the state transition.</param>
    /// <param name="messageBus">The Wolverine message bus.</param>
    /// <param name="logger">The logger.</param>
    public EslDispatchCompletedIntegrationHandler(
        IAccessPointReadRepository accessPointReadRepository,
        IAccessPointWriteRepository accessPointWriteRepository,
        IDisplayAssignmentWriteRepository displayAssignmentWriteRepository,
        IUnitOfWork unitOfWork,
        IMessageBus messageBus,
        ILogger<EslDispatchCompletedIntegrationHandler> logger)
    {
        this.accessPointReadRepository = accessPointReadRepository;
        this.accessPointWriteRepository = accessPointWriteRepository;
        this.displayAssignmentWriteRepository = displayAssignmentWriteRepository;
        this.unitOfWork = unitOfWork;
        this.messageBus = messageBus;
        this.logger = logger;
    }

    /// <summary>
    /// Handles the dispatch-completed integration event.
    /// </summary>
    /// <param name="integrationEvent">The integration event.</param>
    /// <param name="cancellationToken">The cancellation token propagated by the Wolverine runtime.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(EslDispatchCompletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        bool loadDecremented = await this.DecrementAccessPointLoadAsync(integrationEvent.AccessPointSerial, cancellationToken)
            .ConfigureAwait(false);

        DisplayAssignment? assignment = await this.displayAssignmentWriteRepository
            .FindOneAsync(
                assignment => assignment.Id == integrationEvent.AssignmentId,
                enableTracking: true,
                cancellationToken)
            .ConfigureAwait(false);

        if (assignment is null)
        {
            if (loadDecremented)
            {
                await this.unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            LogAssignmentNotFound(this.logger, integrationEvent.AssignmentId, integrationEvent.DisplayId);
            return;
        }

        ErrorOr<Success> transition = assignment.MarkDelivered(integrationEvent.DispatchedAt);
        if (transition.IsError)
        {
            if (loadDecremented)
            {
                await this.unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            LogTransitionRejected(this.logger, assignment.Id, transition.FirstError.Description);
            return;
        }

        await this.unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        DisplayOperationCompleted operationCompleted = new(
            integrationEvent.DisplayId,
            AssignOperationType,
            integrationEvent.DispatchedAt,
            ResultPayload: null);

        await this.messageBus.PublishAsync(operationCompleted).ConfigureAwait(false);

        LogAssignmentDelivered(this.logger, assignment.Id, integrationEvent.EslProvider);
    }

    private async Task<bool> DecrementAccessPointLoadAsync(string? accessPointSerial, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessPointSerial))
        {
            return false;
        }

        AccessPoint? accessPoint = await this.accessPointReadRepository
            .GetBySerialAsync(accessPointSerial, cancellationToken)
            .ConfigureAwait(false);

        if (accessPoint is null)
        {
            LogAccessPointNotFound(this.logger, accessPointSerial);
            return false;
        }

        accessPoint.DecrementLoad();
        await this.accessPointWriteRepository.UpdateAsync(accessPoint, cancellationToken).ConfigureAwait(false);

        await this.messageBus.PublishAsync(new AccessPointLoadChangedIntegrationEvent
        {
            AccessPointId = accessPoint.Id,
            SerialNumber = accessPoint.SerialNumber,
            LocationNodeId = accessPoint.LocationNodeId,
            PreviousLoad = accessPoint.CurrentLoad + 1,
            NewLoad = accessPoint.CurrentLoad,
            MaxCapacity = accessPoint.MaxCapacity,
            ChangedAt = DateTimeOffset.UtcNow,
        }).ConfigureAwait(false);

        return true;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "EslDispatchCompleted received for unknown assignment. AssignmentId={AssignmentId} DisplayId={DisplayId}")]
    private static partial void LogAssignmentNotFound(ILogger logger, Guid assignmentId, Guid displayId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "MarkDelivered rejected for assignment {AssignmentId}: {Reason}")]
    private static partial void LogTransitionRejected(ILogger logger, Guid assignmentId, string reason);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Assignment {AssignmentId} marked Delivered via provider {Provider}")]
    private static partial void LogAssignmentDelivered(ILogger logger, Guid assignmentId, string provider);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Access point {AccessPointSerial} was not found while completing dispatch")]
    private static partial void LogAccessPointNotFound(ILogger logger, string accessPointSerial);
}
