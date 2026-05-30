// <copyright file="AddDisplaysValidatorTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Displays.Features.AddDisplays.V1;
using FluentValidation.TestHelper;

namespace Device.UnitTests.Application.Displays;

public sealed class AddDisplaysValidatorTests
{
    private readonly AddDisplaysValidator _validator = new();

    private static AddDisplaysRequest CreateValidRequest() => new()
    {
        LocationNodeId = "shelf-a1",
        Displays = [new AddDisplayItem("AE-6F-B8-87")],
    };

    [Fact]
    public void Validate_ShouldHaveNoErrors_WhenRequestIsValid()
    {
        // Arrange
        AddDisplaysRequest request = CreateValidRequest();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenLocationNodeIdIsEmpty()
    {
        // Arrange
        AddDisplaysRequest request = CreateValidRequest();
        request.LocationNodeId = string.Empty;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.LocationNodeId);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenLocationNodeIdIsWhitespace()
    {
        // Arrange
        AddDisplaysRequest request = CreateValidRequest();
        request.LocationNodeId = "   ";

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.LocationNodeId);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenLocationNodeIdExceedsMaxLength()
    {
        // Arrange
        AddDisplaysRequest request = CreateValidRequest();
        request.LocationNodeId = new string('x', 201);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.LocationNodeId);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenDisplaysIsEmpty()
    {
        // Arrange
        AddDisplaysRequest request = CreateValidRequest();
        request.Displays = [];

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Displays);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenDisplaysExceedsMaxCount()
    {
        // Arrange
        AddDisplaysRequest request = CreateValidRequest();
        request.Displays = Enumerable.Range(0, 501).Select(_ => new AddDisplayItem("AE-6F-B8-87")).ToList();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Displays);
    }

    [Fact]
    public void Validate_ShouldHaveNoError_WhenDisplaysIsAtMaxCount()
    {
        // Arrange
        AddDisplaysRequest request = CreateValidRequest();
        request.Displays = Enumerable.Range(0, 500).Select(_ => new AddDisplayItem("AE-6F-B8-87")).ToList();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.Displays);
    }

    [Theory]
    [InlineData("AE-6F-B8-87")]
    [InlineData("00-00-00-00")]
    [InlineData("FF-FF-FF-FF")]
    [InlineData("a1-b2-c3-d4")]
    public void Validate_ShouldHaveNoError_WhenShortSerialIsValid(string serial)
    {
        // Arrange
        AddDisplaysRequest request = CreateValidRequest();
        request.Displays = [new AddDisplayItem(serial)];

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor("Displays[0].ShortSerial");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AE6FB887")]
    [InlineData("AE:6F:B8:87")]
    [InlineData("G1-6F-B8-87")]
    [InlineData("AE-6F-B8")]
    [InlineData("AE-6F-B8-87-00")]
    [InlineData("AE_6F_B8_87")]
    public void Validate_ShouldHaveError_WhenShortSerialIsInvalid(string serial)
    {
        // Arrange
        AddDisplaysRequest request = CreateValidRequest();
        request.Displays = [new AddDisplayItem(serial)];

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor("Displays[0].ShortSerial");
    }

    [Fact]
    public void Validate_ShouldHaveMultipleErrors_WhenMultipleDisplaysAreInvalid()
    {
        // Arrange
        AddDisplaysRequest request = CreateValidRequest();
        request.Displays =
        [
            new AddDisplayItem("invalid"),
            new AddDisplayItem(""),
            new AddDisplayItem("AE-6F-B8-87"),
        ];

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor("Displays[0].ShortSerial");
        result.ShouldHaveValidationErrorFor("Displays[1].ShortSerial");
        result.ShouldNotHaveValidationErrorFor("Displays[2].ShortSerial");
    }
}
