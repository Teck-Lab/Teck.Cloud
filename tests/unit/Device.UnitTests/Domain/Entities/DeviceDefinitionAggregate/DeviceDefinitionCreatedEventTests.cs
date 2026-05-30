// <copyright file="DeviceDefinitionCreatedEventTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DeviceDefinitionAggregate;
using Device.Domain.Entities.DeviceDefinitionAggregate.Events;
using ErrorOr;
using SharedKernel.Core.Devices;
using Shouldly;

namespace Device.UnitTests.Domain.Entities.DeviceDefinitionAggregate;

public sealed class DeviceDefinitionCreatedEventTests
{
    [Fact]
    public void Constructor_ShouldSetDeviceDefinitionId()
    {
        // Arrange
        var deviceDefinitionId = Guid.NewGuid();

        // Act
        var domainEvent = new DeviceDefinitionCreatedEvent(deviceDefinitionId, "HS-SE2130R");

        // Assert
        domainEvent.DeviceDefinitionId.ShouldBe(deviceDefinitionId);
    }

    [Fact]
    public void Constructor_ShouldSetModelId()
    {
        // Arrange
        var deviceDefinitionId = Guid.NewGuid();

        // Act
        var domainEvent = new DeviceDefinitionCreatedEvent(deviceDefinitionId, "HS-SE2130R");

        // Assert
        domainEvent.ModelId.ShouldBe("HS-SE2130R");
    }

    [Fact]
    public void Event_ShouldBeRaised_WhenDeviceDefinitionIsCreated()
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
        DeviceDefinition definition = result.Value;

        definition.DomainEvents.Count.ShouldBe(1);
        var domainEvent = definition.DomainEvents.OfType<DeviceDefinitionCreatedEvent>().Single();
        domainEvent.DeviceDefinitionId.ShouldBe(definition.Id);
        domainEvent.ModelId.ShouldBe("HS-SE2130R");
    }

    [Fact]
    public void Event_ShouldIncludeCorrectModelId_ForDifferentModels()
    {
        // Arrange
        var modelId = "SL-SE2870BW";

        // Act
        ErrorOr<DeviceDefinition> result = DeviceDefinition.Create(
            modelId: modelId,
            name: "SoluM 2.87\" BW",
            eslProvider: EslProvider.SoluM,
            supportedColors: DisplayInkColor.Black | DisplayInkColor.White,
            supportsNfc: false,
            widthPx: null,
            heightPx: null,
            catalogManufacturerId: null,
            catalogSupplierId: null,
            catalogProductId: null);

        // Assert
        result.IsError.ShouldBeFalse();
        var domainEvent = result.Value.DomainEvents.OfType<DeviceDefinitionCreatedEvent>().Single();
        domainEvent.ModelId.ShouldBe(modelId);
    }

    [Fact]
    public void Event_ShouldNotBeRaised_WhenCreationFails()
    {
        // Act
        ErrorOr<DeviceDefinition> result = DeviceDefinition.Create(
            modelId: string.Empty,
            name: string.Empty,
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
    }
}
