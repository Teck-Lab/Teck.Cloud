// <copyright file="DeviceDefinitionReadModelMappingTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceDefinitions.ReadModels;
using Device.Domain.Entities.DeviceDefinitionAggregate;
using ErrorOr;
using SharedKernel.Core.Devices;
using Shouldly;

namespace Device.UnitTests.Application.DeviceDefinitions;

public sealed class DeviceDefinitionReadModelMappingTests
{
    [Fact]
    public void MapFromEntity_ShouldPreserveId()
    {
        // Arrange
        ErrorOr<DeviceDefinition> created = DeviceDefinition.Create(
            modelId: "HS-SE2130R",
            name: "Hanshow 2.13\" Red",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black | DisplayInkColor.White | DisplayInkColor.Red,
            supportsNfc: true,
            widthPx: 250,
            heightPx: 122,
            catalogManufacturerId: Guid.NewGuid(),
            catalogSupplierId: Guid.NewGuid(),
            catalogProductId: Guid.NewGuid());

        DeviceDefinition entity = created.Value;

        // Act
        var readModel = new DeviceDefinitionReadModel
        {
            Id = entity.Id,
            ModelId = entity.ModelId,
            Name = entity.Name,
            WidthPx = entity.WidthPx,
            HeightPx = entity.HeightPx,
            SupportedColors = (int)entity.SupportedColors,
            SupportsNfc = entity.SupportsNfc,
            EslProvider = entity.EslProvider.Name,
            CatalogManufacturerId = entity.CatalogManufacturerId,
            CatalogSupplierId = entity.CatalogSupplierId,
            CatalogProductId = entity.CatalogProductId,
        };

        // Assert
        readModel.Id.ShouldBe(entity.Id);
    }

    [Fact]
    public void MapFromEntity_ShouldPreserveModelId()
    {
        // Arrange
        ErrorOr<DeviceDefinition> created = DeviceDefinition.Create(
            modelId: "HS-SE2130R",
            name: "Hanshow 2.13\" Red",
            eslProvider: EslProvider.Hanshow,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        DeviceDefinition entity = created.Value;

        // Act
        var readModel = new DeviceDefinitionReadModel
        {
            Id = entity.Id,
            ModelId = entity.ModelId,
            Name = entity.Name,
            WidthPx = entity.WidthPx,
            HeightPx = entity.HeightPx,
            SupportedColors = (int)entity.SupportedColors,
            SupportsNfc = entity.SupportsNfc,
            EslProvider = entity.EslProvider.Name,
            CatalogManufacturerId = entity.CatalogManufacturerId,
            CatalogSupplierId = entity.CatalogSupplierId,
            CatalogProductId = entity.CatalogProductId,
        };

        // Assert
        readModel.ModelId.ShouldBe("HS-SE2130R");
    }

    [Fact]
    public void MapFromEntity_ShouldPreserveOptionalFields_AsNull()
    {
        // Arrange
        ErrorOr<DeviceDefinition> created = DeviceDefinition.Create(
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

        DeviceDefinition entity = created.Value;

        // Act
        var readModel = new DeviceDefinitionReadModel
        {
            Id = entity.Id,
            ModelId = entity.ModelId,
            Name = entity.Name,
            WidthPx = entity.WidthPx,
            HeightPx = entity.HeightPx,
            SupportedColors = (int)entity.SupportedColors,
            SupportsNfc = entity.SupportsNfc,
            EslProvider = entity.EslProvider.Name,
            CatalogManufacturerId = entity.CatalogManufacturerId,
            CatalogSupplierId = entity.CatalogSupplierId,
            CatalogProductId = entity.CatalogProductId,
        };

        // Assert
        readModel.WidthPx.ShouldBeNull();
        readModel.HeightPx.ShouldBeNull();
        readModel.CatalogManufacturerId.ShouldBeNull();
        readModel.CatalogSupplierId.ShouldBeNull();
        readModel.CatalogProductId.ShouldBeNull();
    }

    [Fact]
    public void MapFromEntity_ShouldPreserveEslProvider_AsString()
    {
        // Arrange
        ErrorOr<DeviceDefinition> created = DeviceDefinition.Create(
            modelId: "SL-001",
            name: "SoluM Model",
            eslProvider: EslProvider.SoluM,
            supportedColors: DisplayInkColor.Black,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        DeviceDefinition entity = created.Value;

        // Act
        var readModel = new DeviceDefinitionReadModel
        {
            Id = entity.Id,
            ModelId = entity.ModelId,
            Name = entity.Name,
            WidthPx = entity.WidthPx,
            HeightPx = entity.HeightPx,
            SupportedColors = (int)entity.SupportedColors,
            SupportsNfc = entity.SupportsNfc,
            EslProvider = entity.EslProvider.Name,
            CatalogManufacturerId = entity.CatalogManufacturerId,
            CatalogSupplierId = entity.CatalogSupplierId,
            CatalogProductId = entity.CatalogProductId,
        };

        // Assert
        readModel.EslProvider.ShouldBe("SoluM");
    }

    [Fact]
    public void MapFromEntity_ShouldPreserveSupportedColors_AsInt()
    {
        // Arrange
        var colors = DisplayInkColor.Black | DisplayInkColor.White | DisplayInkColor.Red | DisplayInkColor.Yellow;
        ErrorOr<DeviceDefinition> created = DeviceDefinition.Create(
            modelId: "HS-001",
            name: "Color Model",
            eslProvider: EslProvider.Hanshow,
            supportedColors: colors,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        DeviceDefinition entity = created.Value;

        // Act
        var readModel = new DeviceDefinitionReadModel
        {
            Id = entity.Id,
            ModelId = entity.ModelId,
            Name = entity.Name,
            WidthPx = entity.WidthPx,
            HeightPx = entity.HeightPx,
            SupportedColors = (int)entity.SupportedColors,
            SupportsNfc = entity.SupportsNfc,
            EslProvider = entity.EslProvider.Name,
            CatalogManufacturerId = entity.CatalogManufacturerId,
            CatalogSupplierId = entity.CatalogSupplierId,
            CatalogProductId = entity.CatalogProductId,
        };

        // Assert
        readModel.SupportedColors.ShouldBe((int)colors);
    }
}
