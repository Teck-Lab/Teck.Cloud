// <copyright file="DisplayTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DisplayAggregate;
using ErrorOr;
using Shouldly;

namespace Device.UnitTests.Domain.Entities.DisplayAggregate;

public sealed class DisplayTests
{
    [Fact]
    public void Create_ShouldReturnValidationError_WhenShortSerialIsEmpty()
    {
        // Act
        ErrorOr<Display> result = Display.Create(
            shortSerial: string.Empty,
            locationNodeId: "shelf-a1",
            deviceDefinitionId: null);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Display.ShortSerialRequired");
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void Create_ShouldReturnValidationError_WhenShortSerialIsWhiteSpace()
    {
        // Act
        ErrorOr<Display> result = Display.Create(
            shortSerial: "   ",
            locationNodeId: "shelf-a1",
            deviceDefinitionId: null);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Display.ShortSerialRequired");
    }

    [Fact]
    public void Create_ShouldReturnValidationError_WhenLocationNodeIdIsEmpty()
    {
        // Act
        ErrorOr<Display> result = Display.Create(
            shortSerial: "AE-6F-B8-87",
            locationNodeId: string.Empty,
            deviceDefinitionId: null);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Display.LocationNodeIdRequired");
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void Create_ShouldReturnValidationError_WhenLocationNodeIdIsWhiteSpace()
    {
        // Act
        ErrorOr<Display> result = Display.Create(
            shortSerial: "AE-6F-B8-87",
            locationNodeId: "   ",
            deviceDefinitionId: null);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Display.LocationNodeIdRequired");
    }

    [Fact]
    public void Create_ShouldReturnDisplay_WithUpperCasedAndTrimmedShortSerial()
    {
        // Act
        ErrorOr<Display> result = Display.Create(
            shortSerial: "  ae-6f-b8-87  ",
            locationNodeId: "shelf-a1",
            deviceDefinitionId: null);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShortSerial.ShouldBe("AE-6F-B8-87");
    }

    [Fact]
    public void Create_ShouldReturnDisplay_WithCorrectProperties_WhenAllParametersAreValid()
    {
        // Arrange
        var definitionId = Guid.NewGuid();

        // Act
        ErrorOr<Display> result = Display.Create(
            shortSerial: "AE-6F-B8-87",
            locationNodeId: "shelf-a1",
            deviceDefinitionId: definitionId);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShortSerial.ShouldBe("AE-6F-B8-87");
        result.Value.LocationNodeId.ShouldBe("shelf-a1");
        result.Value.DeviceDefinitionId.ShouldBe(definitionId);
        result.Value.LongSerial.ShouldBeNull();
        result.Value.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Create_ShouldReturnDisplay_WithNullDeviceDefinitionId_WhenNotProvided()
    {
        // Act
        ErrorOr<Display> result = Display.Create(
            shortSerial: "AE-6F-B8-87",
            locationNodeId: "shelf-a1",
            deviceDefinitionId: null);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.DeviceDefinitionId.ShouldBeNull();
    }

    [Fact]
    public void SetLongSerial_ShouldUpdateLongSerial()
    {
        // Arrange
        Display display = Display.Create(
            shortSerial: "AE-6F-B8-87",
            locationNodeId: "shelf-a1",
            deviceDefinitionId: null).Value;

        // Act
        display.SetLongSerial(229582052926557319L);

        // Assert
        display.LongSerial.ShouldBe(229582052926557319L);
    }

    [Fact]
    public void SetLongSerial_ShouldOverwritePreviousValue()
    {
        // Arrange
        Display display = Display.Create(
            shortSerial: "AE-6F-B8-87",
            locationNodeId: "shelf-a1",
            deviceDefinitionId: null).Value;

        display.SetLongSerial(100L);

        // Act
        display.SetLongSerial(999L);

        // Assert
        display.LongSerial.ShouldBe(999L);
    }
}
