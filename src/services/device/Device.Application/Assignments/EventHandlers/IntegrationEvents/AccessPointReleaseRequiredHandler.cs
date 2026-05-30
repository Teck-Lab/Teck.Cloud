// <copyright file="AccessPointReleaseRequiredHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.AccessPoints;
using SharedKernel.Core.Database;
using SharedKernel.Events;
using Wolverine;

namespace Device.Application.Assignments.EventHandlers.IntegrationEvents;

/// <summary>
/// Consumes <see cref="AccessPointReleaseRequiredIntegrationEvent"/> and releases reserved load on the access point.
/// </summary>
public sealed partial class AccessPointReleaseRequiredHandler
{
    private readonly IAccessPointReadRepository accessPointReadRepository;
    private readonly IAccessPointWriteRepository accessPointWriteRepository;
    private readonly IUnitOfWork unitOfWork;
    private readonly IMessageBus messageBus;
    private readonly ILogger<AccessPointReleaseRequiredHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccessPointReleaseRequiredHandler"/> class.
    /// </summary>
    public AccessPointReleaseRequiredHandler(
        IAccessPointReadRepository accessPointReadRepository,
        IAccessPointWriteRepository accessPointWriteRepository,
        IUnitOfWork unitOfWork,
        IMessageBus messageBus,
        ILogger<AccessPointReleaseRequiredHandler> logger)
    {
        this.accessPointReadRepository = accessPointReadRepository;
        this.accessPointWriteRepository = accessPointWriteRepository;
        this.unitOfWork = unitOfWork;
        this.messageBus = messageBus;
        this.logger = logger;
    }

    /// <summary>
    /// Handles the access-point-release-required integration event.
    /// </summary>
    /// <param name="integrationEvent">The integration event.</param>
    /// <param name="cancellationToken">The cancellation token propagated by Wolverine.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(AccessPointReleaseRequiredIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        AccessPoint? accessPoint = await this.accessPointReadRepository
            .GetBySerialAsync(integrationEvent.AccessPointSerial, cancellationToken)
            .ConfigureAwait(false);

        if (accessPoint is null)
        {
            LogAccessPointNotFound(this.logger, integrationEvent.DisplayId, integrationEvent.AccessPointSerial);
            return;
        }

        int previousLoad = accessPoint.CurrentLoad;
        accessPoint.DecrementLoad();

        await this.accessPointWriteRepository.UpdateAsync(accessPoint, cancellationToken).ConfigureAwait(false);
        await this.unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await this.messageBus.PublishAsync(new AccessPointLoadChangedIntegrationEvent(
            accessPoint.Id,
            accessPoint.SerialNumber,
            accessPoint.LocationNodeId,
            previousLoad,
            accessPoint.CurrentLoad,
            accessPoint.MaxCapacity,
            integrationEvent.ReleasedAt)).ConfigureAwait(false);

        LogAccessPointReleased(this.logger, integrationEvent.DisplayId, accessPoint.SerialNumber, previousLoad, accessPoint.CurrentLoad);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Access point release requested for display {DisplayId}, but AP {AccessPointSerial} was not found")]
    private static partial void LogAccessPointNotFound(ILogger logger, Guid displayId, string accessPointSerial);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Released AP load for display {DisplayId} on AP {AccessPointSerial}. PreviousLoad={PreviousLoad} NewLoad={NewLoad}")]
    private static partial void LogAccessPointReleased(ILogger logger, Guid displayId, string accessPointSerial, int previousLoad, int newLoad);
}
