// <copyright file="EslDispatchFailedIntegrationHandlerTests.cs" company="TeckLab">
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

public sealed class EslDispatchFailedIntegrationHandlerTests
{
    private readonly IAccessPointReadRepository _accessPointReadRepository = Substitute.For<IAccessPointReadRepository>();
    private readonly IAccessPointWriteRepository _accessPointWriteRepository = Substitute.For<IAccessPointWriteRepository>();
    private readonly IDisplayAssignmentWriteRepository _displayAssignmentWriteRepository = Substitute.For<IDisplayAssignmentWriteRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly ILogger<EslDispatchFailedIntegrationHandler> _logger = Substitute.For<ILogger<EslDispatchFailedIntegrationHandler>>();

    [Fact]
    public async Task Handle_ShouldDecrementLoadMarkFailedPublishAndPersist_WhenSuccess()
    {
        // Arrange
        EslDispatchFailedIntegrationHandler handler = CreateHandler();
        AccessPoint accessPoint = CreateAccessPointWithLoad("AP-201", "loc-x", 3, 1);
        DisplayAssignment assignment = CreateRenderedAssignment();
        EslDispatchFailedIntegrationEvent evt = new(assignment.Id, assignment.DisplayId, "Hanshow", "network timeout", DateTimeOffset.UtcNow, "AP-201");

        _accessPointReadRepository.GetBySerialAsync("AP-201", Arg.Any<CancellationToken>()).Returns(accessPoint);
        _displayAssignmentWriteRepository.FindOneAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DisplayAssignment, bool>>>(), true, Arg.Any<CancellationToken>())
            .Returns(assignment);

        // Act
        await handler.Handle(evt, TestContext.Current.CancellationToken);

        // Assert
        accessPoint.CurrentLoad.ShouldBe(0);
        assignment.Status.ShouldBe(DisplayAssignmentStatus.Failed);
        await _messageBus.Received(1).PublishAsync(Arg.Is<AccessPointLoadChangedIntegrationEvent>(e => e.SerialNumber == "AP-201" && e.NewLoad == 0));
        await _messageBus.Received(1).PublishAsync(Arg.Is<DisplayOperationFailed>(e => e.DisplayId == assignment.DisplayId && e.OperationType == "Assign" && e.Reason == "network timeout"));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldStillDecrementAndSave_WhenAssignmentNotFound()
    {
        // Arrange
        EslDispatchFailedIntegrationHandler handler = CreateHandler();
        AccessPoint accessPoint = CreateAccessPointWithLoad("AP-202", "loc-y", 3, 1);
        EslDispatchFailedIntegrationEvent evt = new(Guid.NewGuid(), Guid.NewGuid(), "Hanshow", "vendor-error", DateTimeOffset.UtcNow, "AP-202");

        _accessPointReadRepository.GetBySerialAsync("AP-202", Arg.Any<CancellationToken>()).Returns(accessPoint);
        _displayAssignmentWriteRepository.FindOneAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DisplayAssignment, bool>>>(), true, Arg.Any<CancellationToken>())
            .Returns((DisplayAssignment?)null);

        // Act
        await handler.Handle(evt, TestContext.Current.CancellationToken);

        // Assert
        await _messageBus.Received(1).PublishAsync(Arg.Is<AccessPointLoadChangedIntegrationEvent>(e => e.SerialNumber == "AP-202"));
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<DisplayOperationFailed>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSaveAccessPointDecrementAndNotPublishOperationFailed_WhenMarkFailedRejected()
    {
        // Arrange
        EslDispatchFailedIntegrationHandler handler = CreateHandler();
        AccessPoint accessPoint = CreateAccessPointWithLoad("AP-203", "loc-z", 3, 1);
        DisplayAssignment assignment = CreateDeliveredAssignment();
        EslDispatchFailedIntegrationEvent evt = new(assignment.Id, assignment.DisplayId, "Hanshow", "bad-state", DateTimeOffset.UtcNow, "AP-203");

        _accessPointReadRepository.GetBySerialAsync("AP-203", Arg.Any<CancellationToken>()).Returns(accessPoint);
        _displayAssignmentWriteRepository.FindOneAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DisplayAssignment, bool>>>(), true, Arg.Any<CancellationToken>())
            .Returns(assignment);

        // Act
        await handler.Handle(evt, TestContext.Current.CancellationToken);

        // Assert
        await _messageBus.Received(1).PublishAsync(Arg.Any<AccessPointLoadChangedIntegrationEvent>());
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<DisplayOperationFailed>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private EslDispatchFailedIntegrationHandler CreateHandler() =>
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
        DisplayAssignment assignment = DisplayAssignment.Create(Guid.NewGuid(), "loc-a", "template-1", "Request", null, null, [new DisplayAssignmentZone(1, Guid.NewGuid())]).Value;
        assignment.MarkRendered(new Uri("https://cdn.test/rendered.png"), DateTimeOffset.UtcNow);
        return assignment;
    }

    private static DisplayAssignment CreateDeliveredAssignment()
    {
        DisplayAssignment assignment = CreateRenderedAssignment();
        assignment.MarkDelivered(DateTimeOffset.UtcNow);
        return assignment;
    }
}
