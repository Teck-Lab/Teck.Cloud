// <copyright file="DisplayOperationSagaTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Operations.Saga;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedKernel.Events;
using Shouldly;
using Wolverine;

namespace Device.UnitTests.Application.Operations.Saga;

public sealed class DisplayOperationSagaTests
{
    private readonly IMessageBus _messageBus;
    private readonly ILogger<DisplayOperationSaga> _logger;

    public DisplayOperationSagaTests()
    {
        _messageBus = Substitute.For<IMessageBus>();
        _logger = Substitute.For<ILogger<DisplayOperationSaga>>();
    }

    [Fact]
    public void Start_ShouldSetCurrentOperationAndPublishStarted_WhenFirstOperationArrives()
    {
        // Arrange
        StartDisplayOperation command = CreateStartCommand(operationType: "Assign");

        // Act
        (DisplayOperationSaga saga, DisplayOperationStartedIntegrationEvent started, DisplayOperationStateChangedIntegrationEvent stateChanged, DisplayOperationTimeout timeout) =
            DisplayOperationSaga.Start(command, _logger);

        // Assert
        saga.Id.ShouldBe(command.DisplayId);
        saga.LocationNodeId.ShouldBe(command.LocationNodeId);
        saga.TenantId.ShouldBe(command.TenantId);
        saga.CurrentOperationType.ShouldBe(command.OperationType);
        saga.CurrentOperationStartedAt.ShouldBe(command.RequestedAt);
        saga.Pending.Count.ShouldBe(0);

        started.DisplayId.ShouldBe(command.DisplayId);
        started.LocationNodeId.ShouldBe(command.LocationNodeId);
        started.OperationType.ShouldBe(command.OperationType);
        started.StartedAt.ShouldBe(command.RequestedAt);

        stateChanged.DisplayId.ShouldBe(command.DisplayId);
        stateChanged.LocationNodeId.ShouldBe(command.LocationNodeId);
        stateChanged.TenantId.ShouldBe(command.TenantId);
        stateChanged.OperationType.ShouldBe(command.OperationType);
        stateChanged.Status.ShouldBe("Started");
        stateChanged.QueueDepth.ShouldBe(0);
        stateChanged.Timestamp.ShouldBe(command.RequestedAt);

        timeout.DisplayId.ShouldBe(command.DisplayId);
    }

    [Fact]
    public async Task Handle_ShouldQueueOperationAndPublishQueued_WhenOperationArrivesWhileBusy()
    {
        // Arrange
        StartDisplayOperation active = CreateStartCommand(operationType: "Assign");
        DisplayOperationSaga saga = CreateSaga(active);
        StartDisplayOperation command = CreateStartCommand(active.DisplayId, operationType: "FlashLed");

        // Act
        await saga.Handle(command, _messageBus, _logger);

        // Assert
        saga.CurrentOperationType.ShouldBe(active.OperationType);
        saga.Pending.Count.ShouldBe(1);
        saga.Pending.Single().OperationType.ShouldBe(command.OperationType);

        await _messageBus.Received(1).PublishAsync(Arg.Is<DisplayOperationStateChangedIntegrationEvent>(e =>
            e.DisplayId == command.DisplayId &&
            e.OperationType == command.OperationType &&
            e.Status == "Queued" &&
            e.QueueDepth == 1));
    }

    [Fact]
    public async Task Handle_ShouldPublishCompletedAndClearCurrent_WhenOperationCompletes()
    {
        // Arrange
        StartDisplayOperation active = CreateStartCommand(operationType: "Assign");
        DisplayOperationSaga saga = CreateSaga(active);
        DisplayOperationCompleted completed = new(active.DisplayId, active.OperationType, active.RequestedAt.AddSeconds(5), "{\"ok\":true}");

        // Act
        await saga.Handle(completed, _messageBus, _logger);

        // Assert
        saga.CurrentOperationType.ShouldBeNull();
        saga.CurrentOperationStartedAt.ShouldBeNull();

        await _messageBus.Received(1).PublishAsync(Arg.Is<DisplayOperationCompletedIntegrationEvent>(e =>
            e.DisplayId == completed.DisplayId &&
            e.OperationType == completed.OperationType &&
            e.CompletedAt == completed.CompletedAt &&
            e.ResultPayload == completed.ResultPayload));
        await _messageBus.Received(1).PublishAsync(Arg.Is<DisplayOperationStateChangedIntegrationEvent>(e =>
            e.DisplayId == completed.DisplayId &&
            e.OperationType == completed.OperationType &&
            e.Status == "Completed"));
    }

    [Fact]
    public async Task Handle_ShouldStartNextPendingAndPublishStarted_WhenOperationCompletesWithPending()
    {
        // Arrange
        StartDisplayOperation active = CreateStartCommand(operationType: "Assign");
        DisplayOperationSaga saga = CreateSaga(active);
        PendingOperation pending = new("FlashLed", "{\"color\":\"red\"}", active.RequestedAt.AddSeconds(1));
        saga.Pending.Add(pending);
        DisplayOperationCompleted completed = new(active.DisplayId, active.OperationType, active.RequestedAt.AddSeconds(5), null);

        // Act
        await saga.Handle(completed, _messageBus, _logger);

        // Assert
        saga.CurrentOperationType.ShouldBe(pending.OperationType);
        saga.CurrentOperationStartedAt.ShouldBe(completed.CompletedAt);
        saga.Pending.Count.ShouldBe(0);

        await _messageBus.Received(1).PublishAsync(Arg.Is<DisplayOperationStateChangedIntegrationEvent>(e =>
            e.OperationType == completed.OperationType && e.Status == "Completed"));
        await _messageBus.Received(1).PublishAsync(Arg.Is<DisplayOperationStartedIntegrationEvent>(e =>
            e.DisplayId == active.DisplayId && e.OperationType == pending.OperationType));
        await _messageBus.Received(1).PublishAsync(Arg.Is<DisplayOperationStateChangedIntegrationEvent>(e =>
            e.DisplayId == active.DisplayId && e.OperationType == pending.OperationType && e.Status == "Started"));
    }

    [Fact]
    public async Task Handle_ShouldMarkCompleted_WhenOperationCompletesWithNoPending()
    {
        // Arrange
        StartDisplayOperation active = CreateStartCommand(operationType: "Assign");
        DisplayOperationSaga saga = CreateSaga(active);
        DisplayOperationCompleted completed = new(active.DisplayId, active.OperationType, active.RequestedAt.AddSeconds(5), null);

        // Act
        await saga.Handle(completed, _messageBus, _logger);

        // Assert
        saga.IsCompleted().ShouldBeTrue();
        await _messageBus.Received(1).PublishAsync(Arg.Is<DisplayOperationStateChangedIntegrationEvent>(e => e.Status == "Completed"));
        await _messageBus.DidNotReceive().PublishAsync(Arg.Is<DisplayOperationStateChangedIntegrationEvent>(e => e.Status == "Started"));
    }

    [Fact]
    public async Task Handle_ShouldPublishFailedAndStartNext_WhenOperationFailsWithPending()
    {
        // Arrange
        StartDisplayOperation active = CreateStartCommand(operationType: "Assign");
        DisplayOperationSaga saga = CreateSaga(active);
        PendingOperation pending = new("FlashLed", "{\"color\":\"red\"}", active.RequestedAt.AddSeconds(1));
        saga.Pending.Add(pending);
        DisplayOperationFailed failed = new(active.DisplayId, active.OperationType, active.RequestedAt.AddSeconds(5), "render failed");

        // Act
        await saga.Handle(failed, _messageBus, _logger);

        // Assert
        saga.CurrentOperationType.ShouldBe(pending.OperationType);
        saga.CurrentOperationStartedAt.ShouldBe(failed.FailedAt);
        saga.Pending.Count.ShouldBe(0);

        await _messageBus.Received(1).PublishAsync(Arg.Is<DisplayOperationFailedIntegrationEvent>(e =>
            e.DisplayId == failed.DisplayId &&
            e.OperationType == failed.OperationType &&
            e.Reason == failed.Reason));
        await _messageBus.Received(1).PublishAsync(Arg.Is<DisplayOperationStateChangedIntegrationEvent>(e =>
            e.OperationType == failed.OperationType && e.Status == "Failed"));
        await _messageBus.Received(1).PublishAsync(Arg.Is<DisplayOperationStateChangedIntegrationEvent>(e =>
            e.OperationType == pending.OperationType && e.Status == "Started"));
    }

    [Fact]
    public async Task Handle_ShouldPublishFailed_WhenTimeoutOccurs()
    {
        // Arrange
        StartDisplayOperation active = CreateStartCommand(operationType: "Assign");
        DisplayOperationSaga saga = CreateSaga(active);
        DisplayOperationTimeout timeout = new(active.DisplayId);

        // Act
        await saga.Handle(timeout, _messageBus, _logger);

        // Assert
        saga.CurrentOperationType.ShouldBeNull();
        saga.CurrentOperationStartedAt.ShouldBeNull();

        await _messageBus.Received(1).PublishAsync(Arg.Is<DisplayOperationFailedIntegrationEvent>(e =>
            e.DisplayId == timeout.DisplayId &&
            e.OperationType == active.OperationType &&
            e.Reason == "Display operation timed out."));
        await _messageBus.Received(1).PublishAsync(Arg.Is<DisplayOperationStateChangedIntegrationEvent>(e =>
            e.DisplayId == timeout.DisplayId &&
            e.OperationType == active.OperationType &&
            e.Status == "Failed"));
    }

    [Fact]
    public void NotFound_ShouldLogWarning_WhenStartCommandForMissingSaga()
    {
        // Arrange
        StartDisplayOperation command = CreateStartCommand(operationType: "Assign");

        // Act
        DisplayOperationSaga.NotFound(command, _logger);

        // Assert
        _logger.ReceivedCalls()
            .Count(call => call.GetArguments()[0] is LogLevel.Warning)
            .ShouldBe(1);
    }

    private static DisplayOperationSaga CreateSaga(StartDisplayOperation command)
    {
        return new DisplayOperationSaga
        {
            Id = command.DisplayId,
            LocationNodeId = command.LocationNodeId,
            TenantId = command.TenantId,
            CurrentOperationType = command.OperationType,
            CurrentOperationStartedAt = command.RequestedAt,
        };
    }

    private static StartDisplayOperation CreateStartCommand(Guid? displayId = null, string operationType = "Assign")
    {
        return new StartDisplayOperation(
            displayId ?? Guid.NewGuid(),
            "zone-a",
            "test-tenant",
            operationType,
            "{}",
            DateTimeOffset.UtcNow);
    }
}
