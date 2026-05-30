// <copyright file="DisplayAssignmentRenderedDomainHandlerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.AccessPoints;
using Device.Application.Assignments.Abstractions;
using Device.Application.Assignments.EventHandlers.DomainEvents;
using Device.Application.DeviceDefinitions.Abstractions;
using Device.Application.Operations.Saga;
using DeviceDefinitionReadRepository = Device.Application.DeviceDefinitions.Abstractions.IDeviceDefinitionReadRepository;
using Device.Application.Displays.Abstractions;
using Device.Domain.AccessPoints;
using Device.Domain.Entities.DeviceDefinitionAggregate;
using Device.Domain.Entities.DisplayAggregate;
using Device.Domain.Entities.DisplayAssignmentAggregate.Events;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedKernel.Core.Database;
using SharedKernel.Core.Devices;
using SharedKernel.Events;
using Wolverine;

namespace Device.UnitTests.Application.Assignments;

public sealed class DisplayAssignmentRenderedDomainHandlerTests
{
    private readonly IDisplayWriteRepository _displayWriteRepository;
    private readonly DeviceDefinitionReadRepository _deviceDefinitionReadRepository;
    private readonly IAccessPointReadRepository _accessPointReadRepository;
    private readonly IAccessPointWriteRepository _accessPointWriteRepository;
    private readonly IDisplayAssignmentReadRepository _displayAssignmentReadRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<DisplayAssignmentRenderedDomainHandler> _logger;
    private readonly DisplayAssignmentRenderedDomainHandler _handler;

    public DisplayAssignmentRenderedDomainHandlerTests()
    {
        _displayWriteRepository = Substitute.For<IDisplayWriteRepository>();
        _deviceDefinitionReadRepository = Substitute.For<DeviceDefinitionReadRepository>();
        _accessPointReadRepository = Substitute.For<IAccessPointReadRepository>();
        _accessPointWriteRepository = Substitute.For<IAccessPointWriteRepository>();
        _displayAssignmentReadRepository = Substitute.For<IDisplayAssignmentReadRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _messageBus = Substitute.For<IMessageBus>();
        _logger = Substitute.For<ILogger<DisplayAssignmentRenderedDomainHandler>>();

        ILocationNodeResolver locationNodeResolver = Substitute.For<ILocationNodeResolver>();
        locationNodeResolver.GetAncestorChainAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<string>>([]));

        EffectiveAccessPointResolver resolver = new(
            _displayWriteRepository,
            _deviceDefinitionReadRepository,
            _accessPointReadRepository,
            locationNodeResolver);

        _handler = new DisplayAssignmentRenderedDomainHandler(
            resolver,
            _accessPointWriteRepository,
            _displayAssignmentReadRepository,
            _unitOfWork,
            _messageBus,
            _logger);
    }

    [Fact]
    public async Task Handle_ShouldResolveAccessPointAndPublishEventsAndPersist_WhenSuccess()
    {
        // Arrange
        Guid assignmentId = Guid.NewGuid();
        Guid displayId = Guid.NewGuid();
        Uri renderedImageUri = new("https://cdn.test/rendered.png");
        DisplayAssignmentRenderedEvent domainEvent = new(assignmentId, displayId, renderedImageUri);

        Display display = CreateDisplay(displayId, "loc-a", Guid.NewGuid());
        _displayWriteRepository.FindByIdAsync(displayId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Display?>(display));

        _deviceDefinitionReadRepository.GetByIdAsync(display.DeviceDefinitionId!.Value, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<DeviceDefinitionSnapshot?>(new DeviceDefinitionSnapshot(
                display.DeviceDefinitionId.Value,
                "MODEL-1",
                "Model",
                null,
                null,
                DisplayInkColor.Black,
                false,
                EslProvider.Hanshow,
                null,
                null,
                null)));

        AccessPoint accessPoint = AccessPoint.Create("AP-001", "Hanshow", "loc-a", 5).Value;
        _accessPointReadRepository.FindByVendorAndLocationAsync("Hanshow", "loc-a", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AccessPoint?>(accessPoint));

        Guid renderJobId = Guid.NewGuid();
        _displayAssignmentReadRepository.GetSummaryByIdAsync(assignmentId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<DisplayAssignmentSummary?>(new DisplayAssignmentSummary(
                assignmentId,
                displayId,
                "template-1",
                "Rendered",
                renderJobId,
                renderedImageUri)));

        // Act
        await _handler.Handle(domainEvent);

        // Assert
        await _accessPointWriteRepository.Received(1).UpdateAsync(accessPoint, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        await _messageBus.Received(1).PublishAsync(Arg.Is<SetDisplayOperationAccessPoint>(e =>
            e.DisplayId == displayId && e.AccessPointSerial == "AP-001"));

        await _messageBus.Received(1).PublishAsync(Arg.Is<AccessPointLoadChangedIntegrationEvent>(e =>
            e.SerialNumber == "AP-001" && e.LocationNodeId == "loc-a" && e.PreviousLoad == 0 && e.NewLoad == 1 && e.MaxCapacity == 5));

        await _messageBus.Received(1).PublishAsync(Arg.Is<DisplayAssignmentRenderedIntegrationEvent>(e =>
            e.AssignmentId == assignmentId &&
            e.DisplayId == displayId &&
            e.RenderJobId == renderJobId &&
            e.EslProvider == "Hanshow" &&
            e.AccessPointSerial == "AP-001"));
    }

    [Fact]
    public async Task Handle_ShouldPublishDispatchFailed_WhenNoAccessPointAvailable()
    {
        // Arrange
        Guid assignmentId = Guid.NewGuid();
        Guid displayId = Guid.NewGuid();
        DisplayAssignmentRenderedEvent domainEvent = new(assignmentId, displayId, new Uri("https://cdn.test/a.png"));

        Display display = CreateDisplay(displayId, "loc-b", Guid.NewGuid());
        _displayWriteRepository.FindByIdAsync(displayId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Display?>(display));

        _deviceDefinitionReadRepository.GetByIdAsync(display.DeviceDefinitionId!.Value, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<DeviceDefinitionSnapshot?>(new DeviceDefinitionSnapshot(
                display.DeviceDefinitionId.Value,
                "MODEL-1",
                "Model",
                null,
                null,
                DisplayInkColor.Black,
                false,
                EslProvider.Hanshow,
                null,
                null,
                null)));

        _accessPointReadRepository.FindByVendorAndLocationAsync("Hanshow", "loc-b", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AccessPoint?>(null));

        // Act
        await _handler.Handle(domainEvent);

        // Assert
        await _messageBus.Received(1).PublishAsync(Arg.Is<EslDispatchFailedIntegrationEvent>(e =>
            e.AssignmentId == assignmentId && e.DisplayId == displayId && e.EslProvider == "Hanshow"));

        await _accessPointWriteRepository.DidNotReceive().UpdateAsync(Arg.Any<AccessPoint>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPublishDispatchFailed_WhenAccessPointAtCapacity()
    {
        // Arrange
        Guid assignmentId = Guid.NewGuid();
        Guid displayId = Guid.NewGuid();
        DisplayAssignmentRenderedEvent domainEvent = new(assignmentId, displayId, new Uri("https://cdn.test/a.png"));

        Display display = CreateDisplay(displayId, "loc-c", Guid.NewGuid());
        _displayWriteRepository.FindByIdAsync(displayId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Display?>(display));

        _deviceDefinitionReadRepository.GetByIdAsync(display.DeviceDefinitionId!.Value, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<DeviceDefinitionSnapshot?>(new DeviceDefinitionSnapshot(
                display.DeviceDefinitionId.Value,
                "MODEL-1",
                "Model",
                null,
                null,
                DisplayInkColor.Black,
                false,
                EslProvider.Hanshow,
                null,
                null,
                null)));

        AccessPoint accessPoint = AccessPoint.Create("AP-010", "Hanshow", "loc-c", 1).Value;
        accessPoint.IncrementLoad();
        _accessPointReadRepository.FindByVendorAndLocationAsync("Hanshow", "loc-c", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AccessPoint?>(accessPoint));

        // Act
        await _handler.Handle(domainEvent);

        // Assert
        await _messageBus.Received(1).PublishAsync(Arg.Is<EslDispatchFailedIntegrationEvent>(e =>
            e.AssignmentId == assignmentId && e.DisplayId == displayId && e.EslProvider == "Hanshow" && !string.IsNullOrWhiteSpace(e.Reason)));

        await _accessPointWriteRepository.DidNotReceive().UpdateAsync(Arg.Any<AccessPoint>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static Display CreateDisplay(Guid displayId, string locationNodeId, Guid deviceDefinitionId)
    {
        Display display = Display.Create("AA-BB-CC-DD", locationNodeId, deviceDefinitionId).Value;
        typeof(Display).GetProperty("Id")!.SetValue(display, displayId);
        return display;
    }
}
