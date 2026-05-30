// <copyright file="RegisterDeviceDefinitionCommandHandlerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceDefinitions.Abstractions;
using Device.Application.DeviceDefinitions.Features.RegisterDeviceDefinition.V1;
using Device.Domain.Entities.DeviceDefinitionAggregate;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Database;
using SharedKernel.Core.Devices;
using Shouldly;

#pragma warning disable CA2012

namespace Device.UnitTests.Application.DeviceDefinitions;

public sealed class RegisterDeviceDefinitionCommandHandlerTests
{
    private readonly IDeviceDefinitionWriteRepository _writeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly RegisterDeviceDefinitionCommandHandler _handler;

    public RegisterDeviceDefinitionCommandHandlerTests()
    {
        _writeRepository = Substitute.For<IDeviceDefinitionWriteRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new RegisterDeviceDefinitionCommandHandler(_writeRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ShouldRegisterDefinition_WhenModelIdIsUnique()
    {
        // Arrange
        _writeRepository
            .ExistsWithModelIdAsync("HS-SE2130R", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        var command = new RegisterDeviceDefinitionCommand(
            ModelId: "HS-SE2130R",
            Name: "Hanshow 2.13\" Red",
            EslProvider: EslProvider.Hanshow,
            SupportedColors: DisplayInkColor.Black | DisplayInkColor.White | DisplayInkColor.Red,
            SupportsNfc: true,
            WidthPx: 250,
            HeightPx: 122,
            CatalogManufacturerId: null,
            CatalogSupplierId: null,
            CatalogProductId: null);

        // Act
        ErrorOr<RegisterDeviceDefinitionResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Id.ShouldNotBe(Guid.Empty);

        await _writeRepository.Received(1)
            .AddAsync(Arg.Any<DeviceDefinition>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1)
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenModelIdAlreadyExists()
    {
        // Arrange
        _writeRepository
            .ExistsWithModelIdAsync("HS-SE2130R", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var command = new RegisterDeviceDefinitionCommand(
            ModelId: "HS-SE2130R",
            Name: "Duplicate",
            EslProvider: EslProvider.Hanshow,
            SupportedColors: DisplayInkColor.Black,
            SupportsNfc: false,
            WidthPx: null,
            HeightPx: null,
            CatalogManufacturerId: null,
            CatalogSupplierId: null,
            CatalogProductId: null);

        // Act
        ErrorOr<RegisterDeviceDefinitionResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe("DeviceDefinition.DuplicateModelId");

        await _writeRepository.DidNotReceive()
            .AddAsync(Arg.Any<DeviceDefinition>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive()
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenDomainValidationFails()
    {
        // Arrange
        _writeRepository
            .ExistsWithModelIdAsync(string.Empty, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        var command = new RegisterDeviceDefinitionCommand(
            ModelId: string.Empty,
            Name: string.Empty,
            EslProvider: EslProvider.Hanshow,
            SupportedColors: DisplayInkColor.Black,
            SupportsNfc: false,
            WidthPx: null,
            HeightPx: null,
            CatalogManufacturerId: null,
            CatalogSupplierId: null,
            CatalogProductId: null);

        // Act
        ErrorOr<RegisterDeviceDefinitionResponse> result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Type == ErrorType.Validation);

        await _writeRepository.DidNotReceive()
            .AddAsync(Arg.Any<DeviceDefinition>(), Arg.Any<CancellationToken>());
    }
}

#pragma warning restore CA2012
