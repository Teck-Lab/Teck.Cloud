// <copyright file="RegisterAccessPointValidatorTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.AccessPoints.Features.RegisterAccessPoint.V1;
using FluentValidation.TestHelper;

namespace Device.UnitTests.Application.AccessPoints;

public sealed class RegisterAccessPointValidatorTests
{
    private readonly RegisterAccessPointValidator _validator = new();

    [Fact]
    public void Validate_WhenRequestIsValid_ShouldHaveNoErrors()
    {
        RegisterAccessPointRequest request = new() { SerialNumber = "AP-001", Vendor = "Hanshow", LocationNodeId = "shelf-a1", MaxCapacity = 1 };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenRequiredFieldsMissing_ShouldHaveValidationErrors()
    {
        RegisterAccessPointRequest request = new() { SerialNumber = string.Empty, Vendor = string.Empty, LocationNodeId = string.Empty, MaxCapacity = 0 };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.SerialNumber);
        result.ShouldHaveValidationErrorFor(x => x.Vendor);
        result.ShouldHaveValidationErrorFor(x => x.LocationNodeId);
        result.ShouldHaveValidationErrorFor(x => x.MaxCapacity);
    }

    [Fact]
    public void Validate_WhenFieldsExceedBoundary_ShouldHaveValidationErrors()
    {
        RegisterAccessPointRequest request = new()
        {
            SerialNumber = new string('s', 201),
            Vendor = new string('v', 101),
            LocationNodeId = new string('l', 201),
            MaxCapacity = -1,
        };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.SerialNumber);
        result.ShouldHaveValidationErrorFor(x => x.Vendor);
        result.ShouldHaveValidationErrorFor(x => x.LocationNodeId);
        result.ShouldHaveValidationErrorFor(x => x.MaxCapacity);
    }
}
