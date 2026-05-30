// <copyright file="DisplayAssignmentRenderedIntegrationHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.VendorWorker.Vendors;
using ErrorOr;
using SharedKernel.Events;
using Wolverine;

namespace Device.VendorWorker.EventHandlers;

/// <summary>
/// Consumes <see cref="DisplayAssignmentRenderedIntegrationEvent"/> and dispatches the rendered image
/// via the matching <see cref="IEslDeviceServer"/>, then publishes
/// <see cref="EslDispatchCompletedIntegrationEvent"/> or <see cref="EslDispatchFailedIntegrationEvent"/>
/// back to the Device.Api service. The worker holds no database state - durability falls back to
/// broker-level redelivery and the Device.Api owns the assignment state transitions.
/// </summary>
internal sealed partial class DisplayAssignmentRenderedIntegrationHandler
{
    private readonly IEnumerable<IEslDeviceServer> eslDeviceServers;
    private readonly ILogger<DisplayAssignmentRenderedIntegrationHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayAssignmentRenderedIntegrationHandler"/> class.
    /// </summary>
    /// <param name="eslDeviceServers">All registered vendor ESL adapters. The handler routes by <see cref="IEslDeviceServer.Provider"/>.</param>
    /// <param name="logger">The logger.</param>
    public DisplayAssignmentRenderedIntegrationHandler(
        IEnumerable<IEslDeviceServer> eslDeviceServers,
        ILogger<DisplayAssignmentRenderedIntegrationHandler> logger)
    {
        this.eslDeviceServers = eslDeviceServers;
        this.logger = logger;
    }

    /// <summary>
    /// Handles the rendered integration event by dispatching to the matching vendor adapter
    /// and publishing the dispatch outcome.
    /// </summary>
    /// <param name="integrationEvent">The integration event.</param>
    /// <param name="bus">The Wolverine message bus used to publish the dispatch outcome.</param>
    /// <param name="cancellationToken">The cancellation token propagated by the Wolverine runtime.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(
        DisplayAssignmentRenderedIntegrationEvent integrationEvent,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(bus);

        // Route by provider key. Unknown providers are a no-op so each worker instance only acts on its own vendors.
        IEslDeviceServer? adapter = this.eslDeviceServers.FirstOrDefault(
            candidate => string.Equals(candidate.Provider, integrationEvent.EslProvider, StringComparison.Ordinal));

        if (adapter is null)
        {
            LogProviderUnknown(this.logger, integrationEvent.AssignmentId, integrationEvent.EslProvider);
            return;
        }

        ErrorOr<Success> dispatch = await adapter
            .DispatchAsync(integrationEvent.AssignmentId, integrationEvent.DisplayId, integrationEvent.RenderedImageUri, cancellationToken)
            .ConfigureAwait(false);

        if (dispatch.IsError)
        {
            string reason = dispatch.FirstError.Description;
            LogDispatchFailed(this.logger, integrationEvent.AssignmentId, adapter.Provider, reason);

            await bus.PublishAsync(new EslDispatchFailedIntegrationEvent(
                integrationEvent.AssignmentId,
                integrationEvent.DisplayId,
                adapter.Provider,
                reason,
                DateTimeOffset.UtcNow,
                integrationEvent.AccessPointSerial)).ConfigureAwait(false);
            return;
        }

        LogDispatchCompleted(this.logger, integrationEvent.AssignmentId, adapter.Provider);

        await bus.PublishAsync(new EslDispatchCompletedIntegrationEvent(
            integrationEvent.AssignmentId,
            integrationEvent.DisplayId,
            adapter.Provider,
            DateTimeOffset.UtcNow,
            integrationEvent.AccessPointSerial)).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "DisplayAssignmentRendered skipped for assignment {AssignmentId}: no adapter for provider {EventProvider}")]
    private static partial void LogProviderUnknown(ILogger logger, Guid assignmentId, string eventProvider);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "ESL dispatch failed for assignment {AssignmentId} via {Provider}: {Reason}")]
    private static partial void LogDispatchFailed(ILogger logger, Guid assignmentId, string provider, string reason);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "ESL dispatch completed for assignment {AssignmentId} via {Provider}")]
    private static partial void LogDispatchCompleted(ILogger logger, Guid assignmentId, string provider);
}
