// <copyright file="DeviceLayoutCreatedEventTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DeviceLayoutAggregate;
using Device.Domain.Entities.DeviceLayoutAggregate.Events;
using ErrorOr;
using Shouldly;

namespace Device.UnitTests.Domain.Entities.DeviceLayoutAggregate;

public sealed class DeviceLayoutCreatedEventTests
{
    [Fact]
    public void Constructor_ShouldSetDeviceLayoutId()
    {
        // Arrange
        var deviceLayoutId = Guid.NewGuid();
        var deviceDefinitionId = Guid.NewGuid();

        // Act
        var domainEvent = new DeviceLayoutCreatedEvent(deviceLayoutId, deviceDefinitionId, "3-zone standard");

        // Assert
        domainEvent.DeviceLayoutId.ShouldBe(deviceLayoutId);
    }

    [Fact]
    public void Constructor_ShouldSetDeviceDefinitionId()
    {
        // Arrange
        var deviceLayoutId = Guid.NewGuid();
        var deviceDefinitionId = Guid.NewGuid();

        // Act
        var domainEvent = new DeviceLayoutCreatedEvent(deviceLayoutId, deviceDefinitionId, "3-zone standard");

        // Assert
        domainEvent.DeviceDefinitionId.ShouldBe(deviceDefinitionId);
    }

    [Fact]
    public void Constructor_ShouldSetName()
    {
        // Arrange
        var deviceLayoutId = Guid.NewGuid();
        var deviceDefinitionId = Guid.NewGuid();

        // Act
        var domainEvent = new DeviceLayoutCreatedEvent(deviceLayoutId, deviceDefinitionId, "3-zone standard");

        // Assert
        domainEvent.Name.ShouldBe("3-zone standard");
    }

    [Fact]
    public void Event_ShouldBeRaised_WhenDeviceLayoutIsCreated()
    {
        // Arrange
        var definitionId = Guid.NewGuid();

        // Act
        ErrorOr<DeviceLayout> result = DeviceLayout.Create(
            deviceDefinitionId: definitionId,
            name: "3-zone standard",
            maxZoneCount: 3);

        // Assert
        result.IsError.ShouldBeFalse();
        DeviceLayout layout = result.Value;

        layout.DomainEvents.Count.ShouldBe(1);
        var domainEvent = layout.DomainEvents.OfType<DeviceLayoutCreatedEvent>().Single();
        domainEvent.DeviceLayoutId.ShouldBe(layout.Id);
        domainEvent.DeviceDefinitionId.ShouldBe(definitionId);
        domainEvent.Name.ShouldBe("3-zone standard");
    }

    [Fact]
    public void Event_ShouldIncludeCorrectName_ForDifferentLayouts()
    {
        // Arrange
        var definitionId = Guid.NewGuid();
        var name = "Single-zone compact";

        // Act
        ErrorOr<DeviceLayout> result = DeviceLayout.Create(
            deviceDefinitionId: definitionId,
            name: name,
            maxZoneCount: 1);

        // Assert
        result.IsError.ShouldBeFalse();
        var domainEvent = result.Value.DomainEvents.OfType<DeviceLayoutCreatedEvent>().Single();
        domainEvent.Name.ShouldBe(name);
    }

    [Fact]
    public void Event_ShouldNotBeRaised_WhenCreationFails()
    {
        // Act
        ErrorOr<DeviceLayout> result = DeviceLayout.Create(
            deviceDefinitionId: Guid.Empty,
            name: string.Empty,
            maxZoneCount: 0);

        // Assert
        result.IsError.ShouldBeTrue();
    }
}
