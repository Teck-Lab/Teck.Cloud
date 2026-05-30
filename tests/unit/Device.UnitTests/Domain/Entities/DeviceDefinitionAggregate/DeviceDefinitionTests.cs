// <copyright file="DeviceDefinitionTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DeviceDefinitionAggregate;
using Device.Domain.Entities.DeviceDefinitionAggregate.Events;
using ErrorOr;
using SharedKernel.Core.Devices;
using Shouldly;

namespace Device.UnitTests.Domain.Entities.DeviceDefinitionAggregate;

public sealed class DeviceDefinitionTests
{
    [Fact]
    public void Create_ShouldReturnDefinition_WhenAllRequiredParametersValid()
    {
        // Act
        ErrorOr<DeviceDefinition> result = DeviceDefinition.Create(
            modelId: "HS-SE2130R",
            name: "Hanshow 2.13\" Red",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black | DisplayInkColor.White | DisplayInkColor.Red,
            supportsNfc: true,
            widthPx: 250,
            heightPx: 122,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ModelId.ShouldBe("HS-SE2130R");
        result.Value.Name.ShouldBe("Hanshow 2.13\" Red");
        result.Value.EslProvider.ShouldBe(EslProvider.Hanshow);
        result.Value.SupportedColors.ShouldBe(DisplayInkColor.Black | DisplayInkColor.White | DisplayInkColor.Red);
        result.Value.SupportsNfc.ShouldBeTrue();
        result.Value.WidthPx.ShouldBe(250);
        result.Value.HeightPx.ShouldBe(122);
        result.Value.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Create_ShouldRaiseDeviceDefinitionCreatedEvent()
    {
        // Act
        ErrorOr<DeviceDefinition> result = DeviceDefinition.Create(
            modelId: "HS-SE2130R",
            name: "Hanshow 2.13\" Red",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black | DisplayInkColor.White,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        // Assert
        result.IsError.ShouldBeFalse();
        DeviceDefinition definition = result.Value;

        var domainEvent = definition.DomainEvents.OfType<DeviceDefinitionCreatedEvent>().SingleOrDefault();
        domainEvent.ShouldNotBeNull();
        domainEvent.DeviceDefinitionId.ShouldBe(definition.Id);
        domainEvent.ModelId.ShouldBe("HS-SE2130R");
    }

    [Fact]
    public void Create_ShouldReturnValidationError_WhenModelIdIsEmpty()
    {
        // Act
        ErrorOr<DeviceDefinition> result = DeviceDefinition.Create(
            modelId: string.Empty,
            name: "Some Name",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black | DisplayInkColor.White,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Code == "DeviceDefinition.ModelIdRequired");
    }

    [Fact]
    public void Create_ShouldReturnValidationError_WhenNameIsEmpty()
    {
        // Act
        ErrorOr<DeviceDefinition> result = DeviceDefinition.Create(
            modelId: "HS-SE2130R",
            name: "   ",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black | DisplayInkColor.White,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Code == "DeviceDefinition.NameRequired");
    }

    [Fact]
    public void Create_ShouldReturnMultipleErrors_WhenBothModelIdAndNameEmpty()
    {
        // Act
        ErrorOr<DeviceDefinition> result = DeviceDefinition.Create(
            modelId: string.Empty,
            name: string.Empty,
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black | DisplayInkColor.White,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.Count.ShouldBeGreaterThanOrEqualTo(2);
        result.Errors.ShouldContain(e => e.Code == "DeviceDefinition.ModelIdRequired");
        result.Errors.ShouldContain(e => e.Code == "DeviceDefinition.NameRequired");
    }

    [Fact]
    public void Create_ShouldReturnValidationError_WhenWidthPxIsNegative()
    {
        // Act
        ErrorOr<DeviceDefinition> result = DeviceDefinition.Create(
            modelId: "HS-SE2130R",
            name: "Hanshow 2.13\"",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black | DisplayInkColor.White,
            supportsNfc: false,
            widthPx: -1,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Code == "DeviceDefinition.InvalidWidthPx");
    }

    [Fact]
    public void Create_ShouldReturnValidationError_WhenHeightPxIsZero()
    {
        // Act
        ErrorOr<DeviceDefinition> result = DeviceDefinition.Create(
            modelId: "HS-SE2130R",
            name: "Hanshow 2.13\"",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black | DisplayInkColor.White,
            supportsNfc: false,
            widthPx: null,
            heightPx: 0,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Code == "DeviceDefinition.InvalidHeightPx");
    }

    [Fact]
    public void Create_ShouldSucceed_WhenOptionalFieldsAreNull()
    {
        // Act
        ErrorOr<DeviceDefinition> result = DeviceDefinition.Create(
            modelId: "MIN-001",
            name: "Minimal Model",
            eslProvider: EslProvider.Unknown,
            supportedColors: DisplayInkColor.Black | DisplayInkColor.White,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.WidthPx.ShouldBeNull();
        result.Value.HeightPx.ShouldBeNull();
        result.Value.CatalogManufacturerId.ShouldBeNull();
        result.Value.CatalogSupplierId.ShouldBeNull();
        result.Value.CatalogProductId.ShouldBeNull();
    }

    [Fact]
    public void UpdateCatalogLinks_ShouldSetAllCatalogIds_WhenAllProvided()
    {
        // Arrange
        DeviceDefinition definition = DeviceDefinition.Create(
            modelId: "CAT-001",
            name: "Catalog Test Model",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null).Value;

        var manufacturerId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        // Act
        definition.UpdateCatalogLinks(manufacturerId, supplierId, productId);

        // Assert
        definition.CatalogManufacturerId.ShouldBe(manufacturerId);
        definition.CatalogSupplierId.ShouldBe(supplierId);
        definition.CatalogProductId.ShouldBe(productId);
    }

    [Fact]
    public void UpdateCatalogLinks_ShouldClearAllCatalogIds_WhenNullsProvided()
    {
        // Arrange
        var manufacturerId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        DeviceDefinition definition = DeviceDefinition.Create(
            modelId: "CAT-002",
            name: "Catalog Clear Model",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: manufacturerId,
            catalogSupplierId: supplierId,
            catalogProductId: productId).Value;

        // Act
        definition.UpdateCatalogLinks(null, null, null);

        // Assert
        definition.CatalogManufacturerId.ShouldBeNull();
        definition.CatalogSupplierId.ShouldBeNull();
        definition.CatalogProductId.ShouldBeNull();
    }

    [Fact]
    public void UpdateCatalogLinks_ShouldUpdatePartialLinks_WhenSomeIdsProvided()
    {
        // Arrange
        DeviceDefinition definition = DeviceDefinition.Create(
            modelId: "CAT-003",
            name: "Catalog Partial Model",
            eslProvider: EslProvider.SoluM,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null).Value;

        var manufacturerId = Guid.NewGuid();

        // Act
        definition.UpdateCatalogLinks(manufacturerId, null, null);

        // Assert
        definition.CatalogManufacturerId.ShouldBe(manufacturerId);
        definition.CatalogSupplierId.ShouldBeNull();
        definition.CatalogProductId.ShouldBeNull();
    }
}
