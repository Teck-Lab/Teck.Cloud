// <copyright file="CreateDeviceLayoutCommandHandlerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceDefinitions.Abstractions;
using Device.Application.DeviceLayouts.Abstractions;
using Device.Application.DeviceLayouts.Features.CreateDeviceLayout.V1;
using Device.Domain.Entities.DeviceDefinitionAggregate;
using Device.Domain.Entities.DeviceLayoutAggregate;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Database;
using SharedKernel.Core.Devices;
using Shouldly;

#pragma warning disable CA2012

namespace Device.UnitTests.Application.DeviceLayouts;

public sealed class CreateDeviceLayoutCommandHandlerTests
{
    private readonly IDeviceDefinitionReadRepository _deviceDefinitionReadRepository;
    private readonly IDeviceLayoutWriteRepository _writeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CreateDeviceLayoutCommandHandler _handler;

    public CreateDeviceLayoutCommandHandlerTests()
    {
        _deviceDefinitionReadRepository = Substitute.For<IDeviceDefinitionReadRepository>();
        _writeRepository = Substitute.For<IDeviceLayoutWriteRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new CreateDeviceLayoutCommandHandler(
            _deviceDefinitionReadRepository,
            _writeRepository,
            _unitOfWork);
    }

    [Fact]
    public async Task Handle_ShouldCreateLayout_WhenDefinitionExists()
    {
        // Arrange
        var definitionId = Guid.NewGuid();
        var snapshot = new DeviceDefinitionSnapshot(
            Id: definitionId,
            ModelId: "HS-SE2130R",
            Name: "Hanshow 2.13\"",
            WidthPx: null,
            HeightPx: null,
            SupportedColors: DisplayInkColor.Black,
            SupportsNfc: false,
            EslProvider: EslProvider.Hanshow,
            CatalogManufacturerId: null,
            CatalogSupplierId: null,
            CatalogProductId: null);

        _deviceDefinitionReadRepository
            .GetByIdAsync(definitionId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<DeviceDefinitionSnapshot?>(snapshot));

        var command = new CreateDeviceLayoutCommand(
            DeviceDefinitionId: definitionId,
            Name: "3-zone standard",
            MaxZoneCount: 3);

        // Act
        ErrorOr<CreateDeviceLayoutResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Id.ShouldNotBe(Guid.Empty);

        await _writeRepository.Received(1)
            .AddAsync(Arg.Any<DeviceLayout>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1)
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenDefinitionDoesNotExist()
    {
        // Arrange
        var definitionId = Guid.NewGuid();

        _deviceDefinitionReadRepository
            .GetByIdAsync(definitionId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<DeviceDefinitionSnapshot?>(null));

        var command = new CreateDeviceLayoutCommand(
            DeviceDefinitionId: definitionId,
            Name: "3-zone standard",
            MaxZoneCount: 3);

        // Act
        ErrorOr<CreateDeviceLayoutResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("DeviceDefinition.NotFound");

        await _writeRepository.DidNotReceive()
            .AddAsync(Arg.Any<DeviceLayout>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenMaxZoneCountIsZero()
    {
        // Arrange
        var definitionId = Guid.NewGuid();
        var snapshot = new DeviceDefinitionSnapshot(
            Id: definitionId,
            ModelId: "HS-SE2130R",
            Name: "Hanshow 2.13\"",
            WidthPx: null,
            HeightPx: null,
            SupportedColors: DisplayInkColor.Black,
            SupportsNfc: false,
            EslProvider: EslProvider.Hanshow,
            CatalogManufacturerId: null,
            CatalogSupplierId: null,
            CatalogProductId: null);

        _deviceDefinitionReadRepository
            .GetByIdAsync(definitionId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<DeviceDefinitionSnapshot?>(snapshot));

        var command = new CreateDeviceLayoutCommand(
            DeviceDefinitionId: definitionId,
            Name: "bad-layout",
            MaxZoneCount: 0);

        // Act
        ErrorOr<CreateDeviceLayoutResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Code == "DeviceLayout.InvalidMaxZoneCount");

        await _writeRepository.DidNotReceive()
            .AddAsync(Arg.Any<DeviceLayout>(), Arg.Any<CancellationToken>());
    }
}

#pragma warning restore CA2012
