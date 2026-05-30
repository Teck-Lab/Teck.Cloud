// <copyright file="GetDeviceDefinitionByIdQueryHandlerTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceDefinitions.Abstractions;
using Device.Application.DeviceDefinitions.Features.GetDeviceDefinitionById.V1;
using Device.Domain.Entities.DeviceDefinitionAggregate;
using ErrorOr;
using NSubstitute;
using SharedKernel.Core.Devices;
using Shouldly;

#pragma warning disable CA2012

namespace Device.UnitTests.Application.DeviceDefinitions;

public sealed class GetDeviceDefinitionByIdQueryHandlerTests
{
    private readonly IDeviceDefinitionReadRepository _repository;
    private readonly GetDeviceDefinitionByIdQueryHandler _handler;

    public GetDeviceDefinitionByIdQueryHandlerTests()
    {
        _repository = Substitute.For<IDeviceDefinitionReadRepository>();
        _handler = new GetDeviceDefinitionByIdQueryHandler(_repository);
    }

    [Fact]
    public async Task Handle_ShouldReturnResponse_WhenDefinitionExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        var snapshot = new DeviceDefinitionSnapshot(
            Id: id,
            ModelId: "HS-SE2130R",
            Name: "Hanshow 2.13\" Red",
            WidthPx: 250,
            HeightPx: 122,
            SupportedColors: DisplayInkColor.Black | DisplayInkColor.White | DisplayInkColor.Red,
            SupportsNfc: true,
            EslProvider: EslProvider.Hanshow,
            CatalogManufacturerId: null,
            CatalogSupplierId: null,
            CatalogProductId: null);

        _repository
            .GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<DeviceDefinitionSnapshot?>(snapshot));

        // Act
        ErrorOr<GetDeviceDefinitionByIdResponse> result = await _handler.Handle(
            new GetDeviceDefinitionByIdQuery(id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Id.ShouldBe(id);
        result.Value.ModelId.ShouldBe("HS-SE2130R");
        result.Value.Name.ShouldBe("Hanshow 2.13\" Red");
        result.Value.WidthPx.ShouldBe(250);
        result.Value.HeightPx.ShouldBe(122);
        result.Value.SupportsNfc.ShouldBeTrue();
        result.Value.EslProvider.ShouldBe("Hanshow");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenDefinitionDoesNotExist()
    {
        // Arrange
        var id = Guid.NewGuid();

        _repository
            .GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<DeviceDefinitionSnapshot?>(null));

        // Act
        ErrorOr<GetDeviceDefinitionByIdResponse> result = await _handler.Handle(
            new GetDeviceDefinitionByIdQuery(id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("DeviceDefinition.NotFound");
    }
}

#pragma warning restore CA2012
