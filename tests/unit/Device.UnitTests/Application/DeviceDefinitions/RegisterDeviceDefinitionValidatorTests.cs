// <copyright file="RegisterDeviceDefinitionValidatorTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.DeviceDefinitions.Features.RegisterDeviceDefinition.V1;
using FluentValidation.TestHelper;
using SharedKernel.Core.Devices;

namespace Device.UnitTests.Application.DeviceDefinitions;

public sealed class RegisterDeviceDefinitionValidatorTests
{
    private readonly RegisterDeviceDefinitionValidator _validator = new();

    private static RegisterDeviceDefinitionRequest CreateValidRequest() => new()
    {
        ModelId = "HS-SE2130R",
        Name = "Hanshow 2.13\" Red",
        EslProvider = EslProvider.Hanshow.Name,
        SupportedColors = 7,
        SupportsNfc = true,
        WidthPx = 250,
        HeightPx = 122,
    };

    [Fact]
    public void Validate_ShouldHaveNoErrors_WhenRequestIsValid()
    {
        // Arrange
        RegisterDeviceDefinitionRequest request = CreateValidRequest();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenModelIdIsEmpty()
    {
        // Arrange
        RegisterDeviceDefinitionRequest request = CreateValidRequest();
        request.ModelId = string.Empty;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.ModelId);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenModelIdExceedsMaxLength()
    {
        // Arrange
        RegisterDeviceDefinitionRequest request = CreateValidRequest();
        request.ModelId = new string('x', 101);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.ModelId);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenNameIsEmpty()
    {
        // Arrange
        RegisterDeviceDefinitionRequest request = CreateValidRequest();
        request.Name = string.Empty;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Name);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenNameExceedsMaxLength()
    {
        // Arrange
        RegisterDeviceDefinitionRequest request = CreateValidRequest();
        request.Name = new string('x', 201);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Name);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenEslProviderIsEmpty()
    {
        // Arrange
        RegisterDeviceDefinitionRequest request = CreateValidRequest();
        request.EslProvider = string.Empty;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.EslProvider);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("Hanshow")]
    [InlineData("SoluM")]
    public void Validate_ShouldHaveNoError_WhenEslProviderIsValid(string provider)
    {
        // Arrange
        RegisterDeviceDefinitionRequest request = CreateValidRequest();
        request.EslProvider = provider;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.EslProvider);
    }

    [Theory]
    [InlineData("InvalidProvider")]
    [InlineData("hanshow")]
    [InlineData("UNKNOWN")]
    public void Validate_ShouldHaveError_WhenEslProviderIsInvalid(string provider)
    {
        // Arrange
        RegisterDeviceDefinitionRequest request = CreateValidRequest();
        request.EslProvider = provider;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.EslProvider);
    }

    [Fact]
    public void Validate_ShouldHaveNoError_WhenWidthPxIsNull()
    {
        // Arrange
        RegisterDeviceDefinitionRequest request = CreateValidRequest();
        request.WidthPx = null;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.WidthPx);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(250)]
    [InlineData(int.MaxValue)]
    public void Validate_ShouldHaveNoError_WhenWidthPxIsPositive(int width)
    {
        // Arrange
        RegisterDeviceDefinitionRequest request = CreateValidRequest();
        request.WidthPx = width;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.WidthPx);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Validate_ShouldHaveError_WhenWidthPxIsNotPositive(int width)
    {
        // Arrange
        RegisterDeviceDefinitionRequest request = CreateValidRequest();
        request.WidthPx = width;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.WidthPx);
    }

    [Fact]
    public void Validate_ShouldHaveNoError_WhenHeightPxIsNull()
    {
        // Arrange
        RegisterDeviceDefinitionRequest request = CreateValidRequest();
        request.HeightPx = null;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.HeightPx);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Validate_ShouldHaveError_WhenHeightPxIsNotPositive(int height)
    {
        // Arrange
        RegisterDeviceDefinitionRequest request = CreateValidRequest();
        request.HeightPx = height;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.HeightPx);
    }

    [Fact]
    public void Validate_ShouldHaveNoError_WhenOptionalCatalogIdsAreNull()
    {
        // Arrange
        RegisterDeviceDefinitionRequest request = CreateValidRequest();
        request.CatalogManufacturerId = null;
        request.CatalogSupplierId = null;
        request.CatalogProductId = null;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.CatalogManufacturerId);
        result.ShouldNotHaveValidationErrorFor(r => r.CatalogSupplierId);
        result.ShouldNotHaveValidationErrorFor(r => r.CatalogProductId);
    }

    [Fact]
    public void Validate_ShouldHaveMultipleErrors_WhenMultipleFieldsAreInvalid()
    {
        // Arrange
        RegisterDeviceDefinitionRequest request = new()
        {
            ModelId = string.Empty,
            Name = string.Empty,
            EslProvider = "Invalid",
            WidthPx = -5,
            HeightPx = 0,
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.ModelId);
        result.ShouldHaveValidationErrorFor(r => r.Name);
        result.ShouldHaveValidationErrorFor(r => r.EslProvider);
        result.ShouldHaveValidationErrorFor(r => r.WidthPx);
        result.ShouldHaveValidationErrorFor(r => r.HeightPx);
    }
}
