// <copyright file="EslDispatchCompletedIntegrationHandlerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.Abstractions;
using Device.Application.Assignments.EventHandlers.IntegrationEvents;
using Device.Application.Operations.Saga;
using Device.Domain.AccessPoints;
using Device.Domain.Entities.DisplayAssignmentAggregate;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedKernel.Core.Database;
using SharedKernel.Events;
using Shouldly;
using Wolverine;

namespace Device.UnitTests.Application.Assignments;

public sealed class EslDispatchCompletedIntegrationHandlerTests
{
    private readonly IAccessPointReadRepository _accessPointReadRepository = Substitute.For<IAccessPointReadRepository>();
    private readonly IAccessPointWriteRepository _accessPointWriteRepository = Substitute.For<IAccessPointWriteRepository>();
    private readonly IDisplayAssignmentWriteRepository _displayAssignmentWriteRepository = Substitute.For<IDisplayAssignmentWriteRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly ILogger<EslDispatchCompletedIntegrationHandler> _logger = Substitute.For<ILogger<EslDispatchCompletedIntegrationHandler>>();

    [Fact]
    public async Task Handle_ShouldDecrementLoadMarkDeliveredPublishAndPersist_WhenSuccess()
    {
        // Arrange
        EslDispatchCompletedIntegrationHandler handler = CreateHandler();
        AccessPoint accessPoint = CreateAccessPointWithLoad("AP-001", "loc-a", 3, 1);
        DisplayAssignment assignment = CreateRenderedAssignment();
        EslDispatchCompletedIntegrationEvent evt = new(assignment.Id, assignment.DisplayId, "Hanshow", DateTimeOffset.UtcNow, "AP-001");

        _accessPointReadRepository.GetBySerialAsync("AP-001", Arg.Any<CancellationToken>()).Returns(accessPoint);
        _displayAssignmentWriteRepository.FindOneAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DisplayAssignment, bool>>>(), true, Arg.Any<CancellationToken>())
            .Returns(assignment);

        // Act
        await handler.Handle(evt, TestContext.Current.CancellationToken);

        // Assert
        accessPoint.CurrentLoad.ShouldBe(0);
        assignment.Status.ShouldBe(DisplayAssignmentStatus.Delivered);
        await _messageBus.Received(1).PublishAsync(Arg.Is<AccessPointLoadChangedIntegrationEvent>(e => e.SerialNumber == "AP-001" && e.PreviousLoad == 1 && e.NewLoad == 0));
        await _messageBus.Received(1).PublishAsync(Arg.Is<DisplayOperationCompleted>(e => e.DisplayId == assignment.DisplayId && e.OperationType == "Assign"));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldStillDecrementAndSave_WhenAssignmentNotFound()
    {
        // Arrange
        EslDispatchCompletedIntegrationHandler handler = CreateHandler();
        AccessPoint accessPoint = CreateAccessPointWithLoad("AP-002", "loc-b", 3, 1);
        EslDispatchCompletedIntegrationEvent evt = new(Guid.NewGuid(), Guid.NewGuid(), "Hanshow", DateTimeOffset.UtcNow, "AP-002");

        _accessPointReadRepository.GetBySerialAsync("AP-002", Arg.Any<CancellationToken>()).Returns(accessPoint);
        _displayAssignmentWriteRepository.FindOneAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DisplayAssignment, bool>>>(), true, Arg.Any<CancellationToken>())
            .Returns((DisplayAssignment?)null);

        // Act
        await handler.Handle(evt, TestContext.Current.CancellationToken);

        // Assert
        await _messageBus.Received(1).PublishAsync(Arg.Is<AccessPointLoadChangedIntegrationEvent>(e => e.SerialNumber == "AP-002"));
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<DisplayOperationCompleted>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldStillProcessAssignmentTransition_WhenAccessPointNotFound()
    {
        // Arrange
        EslDispatchCompletedIntegrationHandler handler = CreateHandler();
        DisplayAssignment assignment = CreateRenderedAssignment();
        EslDispatchCompletedIntegrationEvent evt = new(assignment.Id, assignment.DisplayId, "Hanshow", DateTimeOffset.UtcNow, "AP-MISSING");

        _accessPointReadRepository.GetBySerialAsync("AP-MISSING", Arg.Any<CancellationToken>()).Returns((AccessPoint?)null);
        _displayAssignmentWriteRepository.FindOneAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DisplayAssignment, bool>>>(), true, Arg.Any<CancellationToken>())
            .Returns(assignment);

        // Act
        await handler.Handle(evt, TestContext.Current.CancellationToken);

        // Assert
        assignment.Status.ShouldBe(DisplayAssignmentStatus.Delivered);
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<AccessPointLoadChangedIntegrationEvent>());
        await _messageBus.Received(1).PublishAsync(Arg.Any<DisplayOperationCompleted>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSaveAccessPointDecrementAndNotPublishCompleted_WhenMarkDeliveredRejected()
    {
        // Arrange
        EslDispatchCompletedIntegrationHandler handler = CreateHandler();
        AccessPoint accessPoint = CreateAccessPointWithLoad("AP-003", "loc-c", 3, 1);
        DisplayAssignment assignment = CreatePendingAssignment();
        EslDispatchCompletedIntegrationEvent evt = new(assignment.Id, assignment.DisplayId, "Hanshow", DateTimeOffset.UtcNow, "AP-003");

        _accessPointReadRepository.GetBySerialAsync("AP-003", Arg.Any<CancellationToken>()).Returns(accessPoint);
        _displayAssignmentWriteRepository.FindOneAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DisplayAssignment, bool>>>(), true, Arg.Any<CancellationToken>())
            .Returns(assignment);

        // Act
        await handler.Handle(evt, TestContext.Current.CancellationToken);

        // Assert
        assignment.Status.ShouldBe(DisplayAssignmentStatus.Pending);
        await _messageBus.Received(1).PublishAsync(Arg.Any<AccessPointLoadChangedIntegrationEvent>());
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<DisplayOperationCompleted>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private EslDispatchCompletedIntegrationHandler CreateHandler() =>
        new(_accessPointReadRepository, _accessPointWriteRepository, _displayAssignmentWriteRepository, _unitOfWork, _messageBus, _logger);

    private static AccessPoint CreateAccessPointWithLoad(string serial, string location, int maxCapacity, int load)
    {
        AccessPoint accessPoint = AccessPoint.Create(serial, "Hanshow", location, maxCapacity).Value;
        for (int i = 0; i < load; i++)
        {
            accessPoint.IncrementLoad();
        }

        return accessPoint;
    }

    private static DisplayAssignment CreateRenderedAssignment()
    {
        DisplayAssignment assignment = CreatePendingAssignment();
        assignment.MarkRendered(new Uri("https://cdn.test/rendered.png"), DateTimeOffset.UtcNow);
        return assignment;
    }

    private static DisplayAssignment CreatePendingAssignment() =>
        DisplayAssignment.Create(Guid.NewGuid(), "loc-a", "template-1", "Request", null, null, [new DisplayAssignmentZone(1, Guid.NewGuid())]).Value;
}
