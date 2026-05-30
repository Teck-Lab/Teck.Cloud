using Device.Application.Assignments.Abstractions;
using Device.Application.Assignments.Features.PreviewDeviceAssignment.V1;
using ErrorOr;
using NSubstitute;
using Shouldly;

#pragma warning disable CA2012

namespace Device.UnitTests.Application.Assignments;

public sealed class PreviewDeviceAssignmentQueryHandlerTests
{
    private readonly IDeviceDefinitionReadRepository _deviceDefinitionReadRepository;
    private readonly ILocationTemplateContextRunner _locationTemplateContextRunner;
    private readonly PreviewDeviceAssignmentQueryHandler _handler;

    public PreviewDeviceAssignmentQueryHandlerTests()
    {
        _deviceDefinitionReadRepository = Substitute.For<IDeviceDefinitionReadRepository>();
        _locationTemplateContextRunner = Substitute.For<ILocationTemplateContextRunner>();
        _handler = new PreviewDeviceAssignmentQueryHandler(_deviceDefinitionReadRepository, _locationTemplateContextRunner);
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenDeviceIdIsNotAGuid()
    {
        // Arrange
        var query = new PreviewDeviceAssignmentQuery(
            DeviceId: "not-a-guid",
            LocationNodeId: "shelf-a1",
            TemplateId: null,
            Zones: [new PreviewDeviceAssignmentZoneRequest { ZoneIndex = 1, ProductId = Guid.NewGuid().ToString() }]);

        // Act
        ErrorOr<PreviewDeviceAssignmentResponse> result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Code.ShouldBe("Device.InvalidDeviceId");

        await _deviceDefinitionReadRepository.DidNotReceive()
            .GetLayoutContextByDisplayIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenLayoutContextIsMissing()
    {
        // Arrange
        var displayId = Guid.NewGuid();

        var query = new PreviewDeviceAssignmentQuery(
            DeviceId: displayId.ToString(),
            LocationNodeId: "shelf-a1",
            TemplateId: null,
            Zones: [new PreviewDeviceAssignmentZoneRequest { ZoneIndex = 1, ProductId = Guid.NewGuid().ToString() }]);

        _deviceDefinitionReadRepository
            .GetLayoutContextByDisplayIdAsync(displayId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<DisplayLayoutContext?>(null));

        // Act
        ErrorOr<PreviewDeviceAssignmentResponse> result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("Device.LayoutNotFound");
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenZoneIndexesRepeat()
    {
        // Arrange
        var displayId = Guid.NewGuid();

        var query = new PreviewDeviceAssignmentQuery(
            DeviceId: displayId.ToString(),
            LocationNodeId: "shelf-a1",
            TemplateId: null,
            Zones:
            [
                new PreviewDeviceAssignmentZoneRequest { ZoneIndex = 1, ProductId = Guid.NewGuid().ToString() },
                new PreviewDeviceAssignmentZoneRequest { ZoneIndex = 1, ProductId = Guid.NewGuid().ToString() },
            ]);

        _deviceDefinitionReadRepository
            .GetLayoutContextByDisplayIdAsync(displayId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<DisplayLayoutContext?>(new DisplayLayoutContext(displayId, Guid.NewGuid(), MaxZoneCount: 3)));

        // Act
        ErrorOr<PreviewDeviceAssignmentResponse> result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Code.ShouldBe("Device.DuplicateZoneIndex");
    }

    [Fact]
    public async Task Handle_ShouldResolveTemplateFromLocation_WhenTemplateNotProvided()
    {
        // Arrange
        var displayId = Guid.NewGuid();

        var query = new PreviewDeviceAssignmentQuery(
            DeviceId: displayId.ToString(),
            LocationNodeId: "shelf-a1",
            TemplateId: null,
            Zones:
            [
                new PreviewDeviceAssignmentZoneRequest { ZoneIndex = 1, ProductId = Guid.NewGuid().ToString() },
                new PreviewDeviceAssignmentZoneRequest { ZoneIndex = 2, ProductId = Guid.NewGuid().ToString() },
            ]);

        _deviceDefinitionReadRepository
            .GetLayoutContextByDisplayIdAsync(displayId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<DisplayLayoutContext?>(new DisplayLayoutContext(displayId, Guid.NewGuid(), MaxZoneCount: 3)));

        _locationTemplateContextRunner
            .ResolveTemplateContextAsync("shelf-a1", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new LocationTemplateContextSnapshot("shelf-a1", "template-store-default", "Ancestor", 1)));

        // Act
        ErrorOr<PreviewDeviceAssignmentResponse> result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ResolvedTemplateId.ShouldBe("template-store-default");
        result.Value.TemplateSource.ShouldBe("Ancestor");
        result.Value.ZoneCount.ShouldBe(2);
        result.Value.DuplicateProductsAllowed.ShouldBeTrue();

        await _locationTemplateContextRunner.Received(1)
            .ResolveTemplateContextAsync("shelf-a1", Arg.Any<CancellationToken>());
    }
}

#pragma warning restore CA2012
