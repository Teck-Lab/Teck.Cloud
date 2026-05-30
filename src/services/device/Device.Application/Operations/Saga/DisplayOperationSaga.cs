// <copyright file="DisplayOperationSaga.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Events;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace Device.Application.Operations.Saga;

/// <summary>
/// Coordinates sequential execution of display operations so only one operation runs per display.
/// </summary>
public sealed partial class DisplayOperationSaga : Wolverine.Saga
{
    /// <summary>
    /// Gets or sets the saga identity. This is the target display identifier.
    /// </summary>
    [SagaIdentity]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the location node identifier for the display.
    /// </summary>
    public string? LocationNodeId { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier for state-change notifications.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the currently active operation type.
    /// </summary>
    public string? CurrentOperationType { get; set; }

    /// <summary>
    /// Gets or sets when the currently active operation started.
    /// </summary>
    public DateTimeOffset? CurrentOperationStartedAt { get; set; }

    /// <summary>
    /// Gets or sets the reserved access point serial for the active operation.
    /// </summary>
    public string? AccessPointSerial { get; set; }

    /// <summary>
    /// Gets or sets queued operations waiting for the active operation to finish.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "CA2227:Collection properties should be read only",
        Justification = "Wolverine saga storage requires a setter to deserialize queued operations.")]
    public ICollection<PendingOperation> Pending { get; set; } = [];

    /// <summary>
    /// Starts a saga for the first operation targeting a display.
    /// </summary>
    /// <param name="cmd">The start operation command.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>The saga state, started event, state change event, and timeout.</returns>
    public static (
        DisplayOperationSaga Saga,
        DisplayOperationStartedIntegrationEvent Started,
        DisplayOperationStateChangedIntegrationEvent StateChanged,
        DisplayOperationTimeout Timeout) Start(StartDisplayOperation cmd, ILogger<DisplayOperationSaga> logger)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        DisplayOperationSaga saga = new()
        {
            Id = cmd.DisplayId,
            LocationNodeId = cmd.LocationNodeId,
            TenantId = cmd.TenantId,
            CurrentOperationType = cmd.OperationType,
            CurrentOperationStartedAt = cmd.RequestedAt,
        };

        LogOperationStarted(logger, cmd.DisplayId, cmd.OperationType, 0);

        return (
            saga,
            new DisplayOperationStartedIntegrationEvent(cmd.DisplayId, cmd.LocationNodeId, cmd.OperationType, cmd.RequestedAt),
            saga.CreateStateChangedEvent(cmd.OperationType, "Started", 0, cmd.RequestedAt),
            new DisplayOperationTimeout(cmd.DisplayId));
    }

    /// <summary>
    /// Handles subsequent operation requests for an existing display saga.
    /// </summary>
    /// <param name="cmd">The start operation command.</param>
    /// <param name="bus">The Wolverine message bus.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(StartDisplayOperation cmd, IMessageBus bus, ILogger<DisplayOperationSaga> logger)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(bus);

        RefreshContext(cmd.LocationNodeId, cmd.TenantId);

        if (CurrentOperationType is not null)
        {
            Pending.Add(new PendingOperation(cmd.OperationType, cmd.PayloadJson, cmd.RequestedAt));
            LogOperationQueued(logger, cmd.DisplayId, cmd.OperationType, Pending.Count);
            await bus.PublishAsync(CreateStateChangedEvent(cmd.OperationType, "Queued", Pending.Count, cmd.RequestedAt)).ConfigureAwait(false);
            return;
        }

        await StartNextOperationAsync(
            new PendingOperation(cmd.OperationType, cmd.PayloadJson, cmd.RequestedAt),
            bus,
            logger,
            cmd.RequestedAt).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles successful completion of the active operation.
    /// </summary>
    /// <param name="completed">The completion message.</param>
    /// <param name="bus">The Wolverine message bus.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(DisplayOperationCompleted completed, IMessageBus bus, ILogger<DisplayOperationSaga> logger)
    {
        ArgumentNullException.ThrowIfNull(completed);
        ArgumentNullException.ThrowIfNull(bus);

        CurrentOperationType = null;
        CurrentOperationStartedAt = null;
        AccessPointSerial = null;

        await bus.PublishAsync(new DisplayOperationCompletedIntegrationEvent(
            completed.DisplayId,
            completed.OperationType,
            completed.CompletedAt,
            completed.ResultPayload)).ConfigureAwait(false);
        await bus.PublishAsync(CreateStateChangedEvent(completed.OperationType, "Completed", Pending.Count, completed.CompletedAt)).ConfigureAwait(false);
        LogOperationCompleted(logger, completed.DisplayId, completed.OperationType, Pending.Count);

        await StartPendingOrCompleteAsync(bus, logger, completed.CompletedAt).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles failure of the active operation.
    /// </summary>
    /// <param name="failed">The failure message.</param>
    /// <param name="bus">The Wolverine message bus.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(DisplayOperationFailed failed, IMessageBus bus, ILogger<DisplayOperationSaga> logger)
    {
        ArgumentNullException.ThrowIfNull(failed);
        ArgumentNullException.ThrowIfNull(bus);

        CurrentOperationType = null;
        CurrentOperationStartedAt = null;
        AccessPointSerial = null;

        await bus.PublishAsync(new DisplayOperationFailedIntegrationEvent(
            failed.DisplayId,
            failed.OperationType,
            failed.FailedAt,
            failed.Reason)).ConfigureAwait(false);
        await bus.PublishAsync(CreateStateChangedEvent(failed.OperationType, "Failed", Pending.Count, failed.FailedAt)).ConfigureAwait(false);
        LogOperationFailed(logger, failed.DisplayId, failed.OperationType, failed.Reason, Pending.Count);

        await StartPendingOrCompleteAsync(bus, logger, failed.FailedAt).ConfigureAwait(false);
    }

    /// <summary>
    /// Stores the reserved access point serial for the active operation.
    /// </summary>
    /// <param name="msg">The reserved access point message.</param>
    /// <param name="logger">The logger instance.</param>
    public void Handle(SetDisplayOperationAccessPoint msg, ILogger<DisplayOperationSaga> logger)
    {
        ArgumentNullException.ThrowIfNull(msg);

        AccessPointSerial = msg.AccessPointSerial;
    }

    /// <summary>
    /// Handles an operation timeout as a failure and advances the queue.
    /// </summary>
    /// <param name="timeout">The timeout message.</param>
    /// <param name="bus">The Wolverine message bus.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(DisplayOperationTimeout timeout, IMessageBus bus, ILogger<DisplayOperationSaga> logger)
    {
        ArgumentNullException.ThrowIfNull(timeout);
        ArgumentNullException.ThrowIfNull(bus);

        if (CurrentOperationType is null)
        {
            return;
        }

        string timedOutOperation = CurrentOperationType;
        DateTimeOffset failedAt = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(AccessPointSerial))
        {
            await bus.PublishAsync(new AccessPointReleaseRequiredIntegrationEvent(
                Id,
                AccessPointSerial,
                failedAt)).ConfigureAwait(false);

            AccessPointSerial = null;
        }

        CurrentOperationType = null;
        CurrentOperationStartedAt = null;

        await bus.PublishAsync(new DisplayOperationFailedIntegrationEvent(
            timeout.DisplayId,
            timedOutOperation,
            failedAt,
            "Display operation timed out.")).ConfigureAwait(false);
        await bus.PublishAsync(CreateStateChangedEvent(timedOutOperation, "Failed", Pending.Count, failedAt)).ConfigureAwait(false);
        LogOperationTimedOut(logger, timeout.DisplayId, timedOutOperation, Pending.Count);

        await StartPendingOrCompleteAsync(bus, logger, failedAt).ConfigureAwait(false);
    }

    /// <summary>
    /// Logs a discarded start command when Wolverine cannot find an existing saga instance.
    /// </summary>
    /// <param name="cmd">The start command.</param>
    /// <param name="logger">The logger instance.</param>
    public static void NotFound(StartDisplayOperation cmd, ILogger<DisplayOperationSaga> logger)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        LogSagaNotFound(logger, cmd.DisplayId);
    }

    private async Task StartPendingOrCompleteAsync(IMessageBus bus, ILogger<DisplayOperationSaga> logger, DateTimeOffset timestamp)
    {
        if (Pending.Count == 0)
        {
            MarkCompleted();
            return;
        }

        PendingOperation next = Pending.First();
        Pending.Remove(next);

        await StartNextOperationAsync(next, bus, logger, timestamp).ConfigureAwait(false);
    }

    private async Task StartNextOperationAsync(PendingOperation operation, IMessageBus bus, ILogger<DisplayOperationSaga> logger, DateTimeOffset startedAt)
    {
        CurrentOperationType = operation.OperationType;
        CurrentOperationStartedAt = startedAt;

        string locationNodeId = LocationNodeId ?? string.Empty;

        await bus.PublishAsync(new DisplayOperationStartedIntegrationEvent(Id, locationNodeId, operation.OperationType, startedAt)).ConfigureAwait(false);
        await bus.PublishAsync(CreateStateChangedEvent(operation.OperationType, "Started", Pending.Count, startedAt)).ConfigureAwait(false);
        await bus.PublishAsync(new DisplayOperationTimeout(Id)).ConfigureAwait(false);
        LogOperationStarted(logger, Id, operation.OperationType, Pending.Count);
    }

    private void RefreshContext(string locationNodeId, string tenantId)
    {
        if (!string.IsNullOrWhiteSpace(locationNodeId))
        {
            LocationNodeId = locationNodeId;
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            TenantId = tenantId;
        }
    }

    private DisplayOperationStateChangedIntegrationEvent CreateStateChangedEvent(string operationType, string status, int queueDepth, DateTimeOffset timestamp)
    {
        return new DisplayOperationStateChangedIntegrationEvent(
            Id,
            LocationNodeId ?? string.Empty,
            TenantId ?? string.Empty,
            operationType,
            status,
            queueDepth,
            timestamp);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Display operation {OperationType} started for display {DisplayId}. QueueDepth={QueueDepth}")]
    private static partial void LogOperationStarted(ILogger logger, Guid displayId, string operationType, int queueDepth);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Display operation {OperationType} queued for display {DisplayId}. QueueDepth={QueueDepth}")]
    private static partial void LogOperationQueued(ILogger logger, Guid displayId, string operationType, int queueDepth);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Display operation {OperationType} completed for display {DisplayId}. QueueDepth={QueueDepth}")]
    private static partial void LogOperationCompleted(ILogger logger, Guid displayId, string operationType, int queueDepth);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Display operation {OperationType} failed for display {DisplayId}: {Reason}. QueueDepth={QueueDepth}")]
    private static partial void LogOperationFailed(ILogger logger, Guid displayId, string operationType, string reason, int queueDepth);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Display operation {OperationType} timed out for display {DisplayId}. QueueDepth={QueueDepth}")]
    private static partial void LogOperationTimedOut(ILogger logger, Guid displayId, string operationType, int queueDepth);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "DisplayOperationSaga not found for display {DisplayId}")]
    private static partial void LogSagaNotFound(ILogger logger, Guid displayId);
}

/// <summary>
/// A queued display operation waiting for the current operation to finish.
/// </summary>
/// <param name="OperationType">The operation type.</param>
/// <param name="PayloadJson">Operation-specific serialized payload.</param>
/// <param name="RequestedAt">The original request timestamp.</param>
public sealed record PendingOperation(string OperationType, string PayloadJson, DateTimeOffset RequestedAt);
