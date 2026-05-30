// <copyright file="ApplyDeviceAssignmentCommandHandlerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.Abstractions;
using Device.Application.Assignments.Features.ApplyDeviceAssignment.V1;
using Device.Application.Displays.Abstractions;
using Device.Domain.Entities.DisplayAggregate;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Database;
using Shouldly;

#pragma warning disable CA2012

namespace Device.UnitTests.Application.Assignments;

public sealed class ApplyDeviceAssignmentCommandHandlerTests
{
    private readonly IDeviceDefinitionReadRepository _deviceDefinitionReadRepository;
    private readonly IDisplayWriteRepository _displayWriteRepository;
    private readonly IDisplayAssignmentWriteRepository _displayAssignmentWriteRepository;
    private readonly ILocationTemplateContextRunner _locationTemplateContextRunner;
    private readonly IProductSnapshotRunner _productSnapshotRunner;
    private readonly ILabelRenderJobRunner _labelRenderJobRunner;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplyDeviceAssignmentCommandHandler _handler;

    public ApplyDeviceAssignmentCommandHandlerTests()
    {
        _deviceDefinitionReadRepository = Substitute.For<IDeviceDefinitionReadRepository>();
        _displayWriteRepository = Substitute.For<IDisplayWriteRepository>();
        _displayAssignmentWriteRepository = Substitute.For<IDisplayAssignmentWriteRepository>();
        _locationTemplateContextRunner = Substitute.For<ILocationTemplateContextRunner>();
        _productSnapshotRunner = Substitute.For<IProductSnapshotRunner>();
        _labelRenderJobRunner = Substitute.For<ILabelRenderJobRunner>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _handler = new ApplyDeviceAssignmentCommandHandler(
            _deviceDefinitionReadRepository,
            _displayWriteRepository,
            _displayAssignmentWriteRepository,
            _locationTemplateContextRunner,
            _productSnapshotRunner,
            _labelRenderJobRunner,
            _unitOfWork);
    }

    private static Display CreateTestDisplay(Guid displayId)
    {
        ErrorOr<Display> result = Display.Create("TEST-001", "loc-1", null);
        if (result.IsError)
        {
            throw new InvalidOperationException("Failed to create test display");
        }

        typeof(Display).GetProperty("Id")!.SetValue(result.Value, displayId);
        return result.Value;
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenDeviceIdIsNotAGuid()
    {
        // Arrange
        var command = new ApplyDeviceAssignmentCommand(
            DeviceId: "not-a-guid",
            LocationNodeId: "shelf-a1",
            TemplateId: null,
            Zones: [new ApplyDeviceAssignmentZoneRequest { ZoneIndex = 1, ProductId = Guid.NewGuid().ToString() }]);

        // Act
        ErrorOr<ApplyDeviceAssignmentResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Code.ShouldBe("Device.InvalidDeviceIdFormat");

        await _deviceDefinitionReadRepository.DidNotReceive()
            .GetLayoutContextByDisplayIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenLayoutContextIsNull()
    {
        // Arrange
        var displayId = Guid.NewGuid();

        var command = new ApplyDeviceAssignmentCommand(
            DeviceId: displayId.ToString(),
            LocationNodeId: "shelf-a1",
            TemplateId: null,
            Zones: [new ApplyDeviceAssignmentZoneRequest { ZoneIndex = 1, ProductId = Guid.NewGuid().ToString() }]);

        _displayWriteRepository
            .FindByIdAsync(displayId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Display?>(CreateTestDisplay(displayId)));

        _deviceDefinitionReadRepository
            .GetLayoutContextByDisplayIdAsync(displayId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<DisplayLayoutContext?>(null));

        // Act
        ErrorOr<ApplyDeviceAssignmentResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("Device.LayoutNotFound");
    }

    [Fact]
    public async Task Handle_ShouldApplyAssignmentAndQueueRender_WhenRequestIsValid()
    {
        // Arrange
        var displayId = Guid.NewGuid();
        var layoutId = Guid.NewGuid();
        var productIdOne = Guid.NewGuid();
        var productIdTwo = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        var command = new ApplyDeviceAssignmentCommand(
            DeviceId: displayId.ToString(),
            LocationNodeId: "shelf-a1",
            TemplateId: null,
            Zones:
            [
                new ApplyDeviceAssignmentZoneRequest { ZoneIndex = 1, ProductId = productIdOne.ToString() },
                new ApplyDeviceAssignmentZoneRequest { ZoneIndex = 2, ProductId = productIdTwo.ToString() },
            ]);

        _displayWriteRepository
            .FindByIdAsync(displayId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Display?>(CreateTestDisplay(displayId)));

        _deviceDefinitionReadRepository
            .GetLayoutContextByDisplayIdAsync(displayId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<DisplayLayoutContext?>(new DisplayLayoutContext(displayId, layoutId, MaxZoneCount: 3)));

        _locationTemplateContextRunner
            .ResolveTemplateContextAsync("shelf-a1", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new LocationTemplateContextSnapshot("shelf-a1", "template-store-default", "Ancestor", 2)));

        _productSnapshotRunner
            .GetSnapshotsAsync("product", Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<ProductSnapshotItem>>(
            [
                new ProductSnapshotItem(productIdOne, "One", "SKU1", null, "v1"),
                new ProductSnapshotItem(productIdTwo, "Two", "SKU2", null, "v1"),
            ]));

        _labelRenderJobRunner
            .EnqueueAsync(Arg.Any<Guid>(), displayId, "template-store-default", Arg.Any<IReadOnlyCollection<LabelRenderJobZoneItem>>(), Arg.Any<ResolvedTemplateDesignSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new LabelRenderJobResult(jobId, "queued")));

        // Act
        ErrorOr<ApplyDeviceAssignmentResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.DeviceId.ShouldBe(displayId.ToString());
        result.Value.LocationNodeId.ShouldBe("shelf-a1");
        result.Value.ResolvedTemplateId.ShouldBe("template-store-default");
        result.Value.TemplateSource.ShouldBe("Ancestor");
        result.Value.ZoneCount.ShouldBe(2);
        result.Value.DuplicateProductsAllowed.ShouldBeTrue();
        result.Value.RenderJobId.ShouldBe(jobId);
        result.Value.RenderJobStatus.ShouldBe("queued");

        await _productSnapshotRunner.Received(1)
            .GetSnapshotsAsync(
                "product",
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(productIdOne) && ids.Contains(productIdTwo)),
                Arg.Any<CancellationToken>());

        await _labelRenderJobRunner.Received(1)
            .EnqueueAsync(
                Arg.Any<Guid>(),
                displayId,
                "template-store-default",
                Arg.Is<IReadOnlyCollection<LabelRenderJobZoneItem>>(zones =>
                    zones.Count == 2 &&
                    zones.Any(z => z.ZoneIndex == 1 && z.ProductId == productIdOne) &&
                    zones.Any(z => z.ZoneIndex == 2 && z.ProductId == productIdTwo)),
                Arg.Any<ResolvedTemplateDesignSnapshot?>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenProductIdIsInvalidGuid()
    {
        // Arrange
        var displayId = Guid.NewGuid();

        var command = new ApplyDeviceAssignmentCommand(
            DeviceId: displayId.ToString(),
            LocationNodeId: "zone-a",
            TemplateId: "template-zone-b",
            Zones:
            [
                new ApplyDeviceAssignmentZoneRequest { ZoneIndex = 1, ProductId = "not-a-guid" },
            ]);

        _displayWriteRepository
            .FindByIdAsync(displayId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Display?>(CreateTestDisplay(displayId)));

        _deviceDefinitionReadRepository
            .GetLayoutContextByDisplayIdAsync(displayId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<DisplayLayoutContext?>(new DisplayLayoutContext(displayId, Guid.NewGuid(), MaxZoneCount: 2)));

        // Act
        ErrorOr<ApplyDeviceAssignmentResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Code.ShouldBe("Device.InvalidProductIdFormat");

        await _productSnapshotRunner.DidNotReceive()
            .GetSnapshotsAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());

        await _labelRenderJobRunner.DidNotReceive()
            .EnqueueAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<IReadOnlyCollection<LabelRenderJobZoneItem>>(), Arg.Any<ResolvedTemplateDesignSnapshot?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenProductSnapshotIsMissing()
    {
        // Arrange
        var displayId = Guid.NewGuid();
        var missingProductId = Guid.NewGuid();

        var command = new ApplyDeviceAssignmentCommand(
            DeviceId: displayId.ToString(),
            LocationNodeId: "zone-a",
            TemplateId: "template-zone-b",
            Zones:
            [
                new ApplyDeviceAssignmentZoneRequest { ZoneIndex = 1, ProductId = missingProductId.ToString() },
            ]);

        _displayWriteRepository
            .FindByIdAsync(displayId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Display?>(CreateTestDisplay(displayId)));

        _deviceDefinitionReadRepository
            .GetLayoutContextByDisplayIdAsync(displayId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<DisplayLayoutContext?>(new DisplayLayoutContext(displayId, Guid.NewGuid(), MaxZoneCount: 2)));

        _productSnapshotRunner
            .GetSnapshotsAsync("product", Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<ProductSnapshotItem>>([]));

        // Act
        ErrorOr<ApplyDeviceAssignmentResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Code.ShouldBe("Device.ProductsNotFound");

        await _labelRenderJobRunner.DidNotReceive()
            .EnqueueAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<IReadOnlyCollection<LabelRenderJobZoneItem>>(), Arg.Any<ResolvedTemplateDesignSnapshot?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenTemplateCannotBeResolved()
    {
        // Arrange
        var displayId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var command = new ApplyDeviceAssignmentCommand(
            DeviceId: displayId.ToString(),
            LocationNodeId: "unknown-node",
            TemplateId: null,
            Zones:
            [
                new ApplyDeviceAssignmentZoneRequest { ZoneIndex = 1, ProductId = productId.ToString() },
            ]);

        _displayWriteRepository
            .FindByIdAsync(displayId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Display?>(CreateTestDisplay(displayId)));

        _deviceDefinitionReadRepository
            .GetLayoutContextByDisplayIdAsync(displayId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<DisplayLayoutContext?>(new DisplayLayoutContext(displayId, Guid.NewGuid(), MaxZoneCount: 2)));

        _productSnapshotRunner
            .GetSnapshotsAsync("product", Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<ProductSnapshotItem>>(
            [
                new ProductSnapshotItem(productId, "Sample", null, null, "v1"),
            ]));

        _locationTemplateContextRunner
            .ResolveTemplateContextAsync("unknown-node", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new LocationTemplateContextSnapshot("unknown-node", null, "None", 0)));

        // Act
        ErrorOr<ApplyDeviceAssignmentResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Code.ShouldBe("Device.TemplateNotResolved");

        await _labelRenderJobRunner.DidNotReceive()
            .EnqueueAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<IReadOnlyCollection<LabelRenderJobZoneItem>>(), Arg.Any<ResolvedTemplateDesignSnapshot?>(), Arg.Any<CancellationToken>());
    }
}

#pragma warning restore CA2012
