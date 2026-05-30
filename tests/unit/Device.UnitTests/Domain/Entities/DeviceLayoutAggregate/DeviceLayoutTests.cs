// <copyright file="DeviceLayoutTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DeviceLayoutAggregate;
using Device.Domain.Entities.DeviceLayoutAggregate.Events;
using ErrorOr;
using Shouldly;

namespace Device.UnitTests.Domain.Entities.DeviceLayoutAggregate;

public sealed class DeviceLayoutTests
{
    [Fact]
    public void Create_ShouldReturnLayout_WhenAllParametersValid()
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
        result.Value.DeviceDefinitionId.ShouldBe(definitionId);
        result.Value.Name.ShouldBe("3-zone standard");
        result.Value.MaxZoneCount.ShouldBe(3);
        result.Value.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Create_ShouldRaiseDeviceLayoutCreatedEvent()
    {
        // Arrange
        var definitionId = Guid.NewGuid();

        // Act
        ErrorOr<DeviceLayout> result = DeviceLayout.Create(
            deviceDefinitionId: definitionId,
            name: "Single-zone",
            maxZoneCount: 1);

        // Assert
        result.IsError.ShouldBeFalse();
        DeviceLayout layout = result.Value;

        var domainEvent = layout.DomainEvents.OfType<DeviceLayoutCreatedEvent>().SingleOrDefault();
        domainEvent.ShouldNotBeNull();
        domainEvent.DeviceLayoutId.ShouldBe(layout.Id);
        domainEvent.DeviceDefinitionId.ShouldBe(definitionId);
        domainEvent.Name.ShouldBe("Single-zone");
    }

    [Fact]
    public void Create_ShouldReturnValidationError_WhenDeviceDefinitionIdIsEmpty()
    {
        // Act
        ErrorOr<DeviceLayout> result = DeviceLayout.Create(
            deviceDefinitionId: Guid.Empty,
            name: "3-zone standard",
            maxZoneCount: 3);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Code == "DeviceLayout.DeviceDefinitionIdRequired");
    }

    [Fact]
    public void Create_ShouldReturnValidationError_WhenNameIsEmpty()
    {
        // Act
        ErrorOr<DeviceLayout> result = DeviceLayout.Create(
            deviceDefinitionId: Guid.NewGuid(),
            name: string.Empty,
            maxZoneCount: 3);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Code == "DeviceLayout.NameRequired");
    }

    [Fact]
    public void Create_ShouldReturnValidationError_WhenNameIsWhitespace()
    {
        // Act
        ErrorOr<DeviceLayout> result = DeviceLayout.Create(
            deviceDefinitionId: Guid.NewGuid(),
            name: "   ",
            maxZoneCount: 3);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Code == "DeviceLayout.NameRequired");
    }

    [Fact]
    public void Create_ShouldReturnValidationError_WhenMaxZoneCountIsZero()
    {
        // Act
        ErrorOr<DeviceLayout> result = DeviceLayout.Create(
            deviceDefinitionId: Guid.NewGuid(),
            name: "3-zone standard",
            maxZoneCount: 0);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Code == "DeviceLayout.InvalidMaxZoneCount");
    }

    [Fact]
    public void Create_ShouldReturnValidationError_WhenMaxZoneCountIsNegative()
    {
        // Act
        ErrorOr<DeviceLayout> result = DeviceLayout.Create(
            deviceDefinitionId: Guid.NewGuid(),
            name: "3-zone standard",
            maxZoneCount: -1);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Code == "DeviceLayout.InvalidMaxZoneCount");
    }

    [Fact]
    public void Create_ShouldTrimName_WhenNameHasLeadingOrTrailingWhitespace()
    {
        // Act
        ErrorOr<DeviceLayout> result = DeviceLayout.Create(
            deviceDefinitionId: Guid.NewGuid(),
            name: "  3-zone standard  ",
            maxZoneCount: 2);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Name.ShouldBe("3-zone standard");
    }
}
