// <copyright file="DeviceLayoutEdgeCaseTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DeviceLayoutAggregate;
using ErrorOr;
using Shouldly;

namespace Device.UnitTests.Domain.Entities.DeviceLayoutAggregate;

public sealed class DeviceLayoutEdgeCaseTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public void Create_ShouldReturnValidationError_WhenMaxZoneCountIsNotPositive(int maxZoneCount)
    {
        // Act
        ErrorOr<DeviceLayout> result = DeviceLayout.Create(
            deviceDefinitionId: Guid.NewGuid(),
            name: "Valid Name",
            maxZoneCount: maxZoneCount);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Code == "DeviceLayout.InvalidMaxZoneCount");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(int.MaxValue)]
    public void Create_ShouldSucceed_WhenMaxZoneCountIsPositive(int maxZoneCount)
    {
        // Act
        ErrorOr<DeviceLayout> result = DeviceLayout.Create(
            deviceDefinitionId: Guid.NewGuid(),
            name: "Valid Name",
            maxZoneCount: maxZoneCount);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.MaxZoneCount.ShouldBe(maxZoneCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Create_ShouldReturnValidationError_WhenNameIsNullOrWhitespace(string name)
    {
        // Act
        ErrorOr<DeviceLayout> result = DeviceLayout.Create(
            deviceDefinitionId: Guid.NewGuid(),
            name: name,
            maxZoneCount: 3);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Code == "DeviceLayout.NameRequired");
    }

    [Fact]
    public void Create_ShouldReturnValidationError_WhenDeviceDefinitionIdIsEmpty()
    {
        // Act
        ErrorOr<DeviceLayout> result = DeviceLayout.Create(
            deviceDefinitionId: Guid.Empty,
            name: "Valid Name",
            maxZoneCount: 3);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Code == "DeviceLayout.DeviceDefinitionIdRequired");
    }

    [Fact]
    public void Create_ShouldReturnMultipleErrors_WhenMultipleFieldsAreInvalid()
    {
        // Act
        ErrorOr<DeviceLayout> result = DeviceLayout.Create(
            deviceDefinitionId: Guid.Empty,
            name: string.Empty,
            maxZoneCount: 0);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.Count.ShouldBe(3);
        result.Errors.ShouldContain(e => e.Code == "DeviceLayout.DeviceDefinitionIdRequired");
        result.Errors.ShouldContain(e => e.Code == "DeviceLayout.NameRequired");
        result.Errors.ShouldContain(e => e.Code == "DeviceLayout.InvalidMaxZoneCount");
    }

    [Fact]
    public void Create_ShouldSucceed_WhenMaxZoneCountIsOne()
    {
        // Act
        ErrorOr<DeviceLayout> result = DeviceLayout.Create(
            deviceDefinitionId: Guid.NewGuid(),
            name: "Single-zone",
            maxZoneCount: 1);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.MaxZoneCount.ShouldBe(1);
    }

    [Fact]
    public void Create_ShouldTrimName_WithRegularWhitespace()
    {
        // Act
        ErrorOr<DeviceLayout> result = DeviceLayout.Create(
            deviceDefinitionId: Guid.NewGuid(),
            name: "  3-zone standard  ",
            maxZoneCount: 3);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Name.ShouldBe("3-zone standard");
    }
}
