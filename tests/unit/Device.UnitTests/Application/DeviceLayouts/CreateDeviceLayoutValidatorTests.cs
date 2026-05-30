// <copyright file="CreateDeviceLayoutValidatorTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceLayouts.Features.CreateDeviceLayout.V1;
using FluentValidation.TestHelper;

namespace Device.UnitTests.Application.DeviceLayouts;

public sealed class CreateDeviceLayoutValidatorTests
{
    private readonly CreateDeviceLayoutValidator _validator = new();

    private static CreateDeviceLayoutRequest CreateValidRequest() => new()
    {
        DeviceDefinitionId = Guid.NewGuid(),
        Name = "3-zone standard",
        MaxZoneCount = 3,
    };

    [Fact]
    public void Validate_ShouldHaveNoErrors_WhenRequestIsValid()
    {
        // Arrange
        CreateDeviceLayoutRequest request = CreateValidRequest();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenDeviceDefinitionIdIsEmpty()
    {
        // Arrange
        CreateDeviceLayoutRequest request = CreateValidRequest();
        request.DeviceDefinitionId = Guid.Empty;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.DeviceDefinitionId);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenNameIsEmpty()
    {
        // Arrange
        CreateDeviceLayoutRequest request = CreateValidRequest();
        request.Name = string.Empty;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Name);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenNameIsWhitespace()
    {
        // Arrange
        CreateDeviceLayoutRequest request = CreateValidRequest();
        request.Name = "   ";

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Name);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenNameExceedsMaxLength()
    {
        // Arrange
        CreateDeviceLayoutRequest request = CreateValidRequest();
        request.Name = new string('x', 201);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public void Validate_ShouldHaveError_WhenMaxZoneCountIsNotPositive(int maxZoneCount)
    {
        // Arrange
        CreateDeviceLayoutRequest request = CreateValidRequest();
        request.MaxZoneCount = maxZoneCount;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.MaxZoneCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void Validate_ShouldHaveNoError_WhenMaxZoneCountIsPositive(int maxZoneCount)
    {
        // Arrange
        CreateDeviceLayoutRequest request = CreateValidRequest();
        request.MaxZoneCount = maxZoneCount;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.MaxZoneCount);
    }

    [Fact]
    public void Validate_ShouldHaveMultipleErrors_WhenMultipleFieldsAreInvalid()
    {
        // Arrange
        CreateDeviceLayoutRequest request = new()
        {
            DeviceDefinitionId = Guid.Empty,
            Name = string.Empty,
            MaxZoneCount = 0,
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.DeviceDefinitionId);
        result.ShouldHaveValidationErrorFor(r => r.Name);
        result.ShouldHaveValidationErrorFor(r => r.MaxZoneCount);
    }
}
