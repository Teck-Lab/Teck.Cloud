// <copyright file="DeviceDefinitionEdgeCaseTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DeviceDefinitionAggregate;
using ErrorOr;
using SharedKernel.Core.Devices;
using Shouldly;

namespace Device.UnitTests.Domain.Entities.DeviceDefinitionAggregate;

public sealed class DeviceDefinitionEdgeCaseTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Create_ShouldReturnValidationError_WhenModelIdIsNullOrWhitespace(string modelId)
    {
        // Act
        ErrorOr<DeviceDefinition> result = DeviceDefinition.Create(
            modelId: modelId!,
            name: "Valid Name",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black,
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n")]
    public void Create_ShouldReturnValidationError_WhenNameIsNullOrWhitespace(string? name)
    {
        // Act
        ErrorOr<DeviceDefinition> result = DeviceDefinition.Create(
            modelId: "HS-001",
            name: name!,
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black,
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

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-2147483648)]
    public void Create_ShouldReturnValidationError_WhenWidthPxIsNotPositive(int width)
    {
        // Act
        ErrorOr<DeviceDefinition> result = DeviceDefinition.Create(
            modelId: "HS-001",
            name: "Valid Name",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: false,
            widthPx: width,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Code == "DeviceDefinition.InvalidWidthPx");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Create_ShouldReturnValidationError_WhenHeightPxIsNotPositive(int height)
    {
        // Act
        ErrorOr<DeviceDefinition> result = DeviceDefinition.Create(
            modelId: "HS-001",
            name: "Valid Name",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: false,
            widthPx: null,
            heightPx: height,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Code == "DeviceDefinition.InvalidHeightPx");
    }

    [Fact]
    public void Create_ShouldTrimModelId_WhenModelIdHasLeadingOrTrailingWhitespace()
    {
        // Act
        ErrorOr<DeviceDefinition> result = DeviceDefinition.Create(
            modelId: "  HS-SE2130R  ",
            name: "Hanshow 2.13\" Red",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ModelId.ShouldBe("HS-SE2130R");
    }

    [Fact]
    public void Create_ShouldTrimName_WhenNameHasLeadingOrTrailingWhitespace()
    {
        // Act
        ErrorOr<DeviceDefinition> result = DeviceDefinition.Create(
            modelId: "HS-SE2130R",
            name: "  Hanshow 2.13\" Red  ",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Name.ShouldBe("Hanshow 2.13\" Red");
    }

    [Fact]
    public void Create_ShouldReturnAllErrors_WhenMultipleFieldsAreInvalid()
    {
        // Act
        ErrorOr<DeviceDefinition> result = DeviceDefinition.Create(
            modelId: string.Empty,
            name: string.Empty,
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: false,
            widthPx: -5,
            heightPx: 0,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.Count.ShouldBeGreaterThanOrEqualTo(4);
        result.Errors.ShouldContain(e => e.Code == "DeviceDefinition.ModelIdRequired");
        result.Errors.ShouldContain(e => e.Code == "DeviceDefinition.NameRequired");
        result.Errors.ShouldContain(e => e.Code == "DeviceDefinition.InvalidWidthPx");
        result.Errors.ShouldContain(e => e.Code == "DeviceDefinition.InvalidHeightPx");
    }

    [Fact]
    public void Create_ShouldSucceed_WhenAllOptionalCatalogIdsAreProvided()
    {
        // Arrange
        var manufacturerId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        // Act
        ErrorOr<DeviceDefinition> result = DeviceDefinition.Create(
            modelId: "CAT-FULL-001",
            name: "Full Catalog Model",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black | DisplayInkColor.White,
            supportsNfc: true,
            widthPx: 250,
            heightPx: 122,
            catalogManufacturerId: manufacturerId,
            catalogSupplierId: supplierId,
            catalogProductId: productId);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.CatalogManufacturerId.ShouldBe(manufacturerId);
        result.Value.CatalogSupplierId.ShouldBe(supplierId);
        result.Value.CatalogProductId.ShouldBe(productId);
    }

    [Fact]
    public void Create_ShouldPreserveEslProvider_WhenProviderIsHanshow()
    {
        // Act
        ErrorOr<DeviceDefinition> result = DeviceDefinition.Create(
            modelId: "HS-001",
            name: "Test Model",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.EslProvider.ShouldBe(EslProvider.Hanshow);
    }

    [Fact]
    public void Create_ShouldPreserveEslProvider_WhenProviderIsSoluM()
    {
        // Act
        ErrorOr<DeviceDefinition> result = DeviceDefinition.Create(
            modelId: "SL-001",
            name: "Test Model",
            eslProvider: EslProvider.SoluM,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.EslProvider.ShouldBe(EslProvider.SoluM);
    }

    [Fact]
    public void Create_ShouldPreserveEslProvider_WhenProviderIsUnknown()
    {
        // Act
        ErrorOr<DeviceDefinition> result = DeviceDefinition.Create(
            modelId: "UNK-001",
            name: "Test Model",
            eslProvider: EslProvider.Unknown,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.EslProvider.ShouldBe(EslProvider.Unknown);
    }
}
