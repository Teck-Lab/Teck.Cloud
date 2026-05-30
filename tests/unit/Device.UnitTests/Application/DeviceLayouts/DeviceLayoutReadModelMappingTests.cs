// <copyright file="DeviceLayoutReadModelMappingTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceLayouts.ReadModels;
using Device.Domain.Entities.DeviceLayoutAggregate;
using ErrorOr;
using Shouldly;

namespace Device.UnitTests.Application.DeviceLayouts;

public sealed class DeviceLayoutReadModelMappingTests
{
    [Fact]
    public void MapFromEntity_ShouldPreserveId()
    {
        // Arrange
        var definitionId = Guid.NewGuid();
        ErrorOr<DeviceLayout> created = DeviceLayout.Create(
            deviceDefinitionId: definitionId,
            name: "3-zone standard",
            maxZoneCount: 3);

        DeviceLayout entity = created.Value;

        // Act
        var readModel = new DeviceLayoutReadModel
        {
            Id = entity.Id,
            DeviceDefinitionId = entity.DeviceDefinitionId,
            Name = entity.Name,
            MaxZoneCount = entity.MaxZoneCount,
        };

        // Assert
        readModel.Id.ShouldBe(entity.Id);
    }

    [Fact]
    public void MapFromEntity_ShouldPreserveDeviceDefinitionId()
    {
        // Arrange
        var definitionId = Guid.NewGuid();
        ErrorOr<DeviceLayout> created = DeviceLayout.Create(
            deviceDefinitionId: definitionId,
            name: "3-zone standard",
            maxZoneCount: 3);

        DeviceLayout entity = created.Value;

        // Act
        var readModel = new DeviceLayoutReadModel
        {
            Id = entity.Id,
            DeviceDefinitionId = entity.DeviceDefinitionId,
            Name = entity.Name,
            MaxZoneCount = entity.MaxZoneCount,
        };

        // Assert
        readModel.DeviceDefinitionId.ShouldBe(definitionId);
    }

    [Fact]
    public void MapFromEntity_ShouldPreserveName()
    {
        // Arrange
        var definitionId = Guid.NewGuid();
        ErrorOr<DeviceLayout> created = DeviceLayout.Create(
            deviceDefinitionId: definitionId,
            name: "Single-zone compact",
            maxZoneCount: 1);

        DeviceLayout entity = created.Value;

        // Act
        var readModel = new DeviceLayoutReadModel
        {
            Id = entity.Id,
            DeviceDefinitionId = entity.DeviceDefinitionId,
            Name = entity.Name,
            MaxZoneCount = entity.MaxZoneCount,
        };

        // Assert
        readModel.Name.ShouldBe("Single-zone compact");
    }

    [Fact]
    public void MapFromEntity_ShouldPreserveMaxZoneCount()
    {
        // Arrange
        var definitionId = Guid.NewGuid();
        ErrorOr<DeviceLayout> created = DeviceLayout.Create(
            deviceDefinitionId: definitionId,
            name: "5-zone extended",
            maxZoneCount: 5);

        DeviceLayout entity = created.Value;

        // Act
        var readModel = new DeviceLayoutReadModel
        {
            Id = entity.Id,
            DeviceDefinitionId = entity.DeviceDefinitionId,
            Name = entity.Name,
            MaxZoneCount = entity.MaxZoneCount,
        };

        // Assert
        readModel.MaxZoneCount.ShouldBe(5);
    }

    [Fact]
    public void MapFromEntity_ShouldPreserveName_AfterTrimming()
    {
        // Arrange
        var definitionId = Guid.NewGuid();
        ErrorOr<DeviceLayout> created = DeviceLayout.Create(
            deviceDefinitionId: definitionId,
            name: "  3-zone standard  ",
            maxZoneCount: 3);

        DeviceLayout entity = created.Value;

        // Act
        var readModel = new DeviceLayoutReadModel
        {
            Id = entity.Id,
            DeviceDefinitionId = entity.DeviceDefinitionId,
            Name = entity.Name,
            MaxZoneCount = entity.MaxZoneCount,
        };

        // Assert
        readModel.Name.ShouldBe("3-zone standard");
    }
}
