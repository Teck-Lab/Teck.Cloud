// <copyright file="UpdateAccessPointStatusValidatorTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.AccessPoints.Features.UpdateAccessPointStatus.V1;
using FluentValidation.TestHelper;

namespace Device.UnitTests.Application.AccessPoints;

public sealed class UpdateAccessPointStatusValidatorTests
{
    private readonly UpdateAccessPointStatusValidator _validator = new();

    [Theory]
    [InlineData("Online")]
    [InlineData("Offline")]
    [InlineData("Maintenance")]
    public void Validate_WhenStatusAndSerialProvided_ShouldPass(string status)
    {
        UpdateAccessPointStatusRequest request = new() { Serial = "AP-001", Status = status };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenSerialMissing_ShouldFail()
    {
        UpdateAccessPointStatusRequest request = new() { Serial = string.Empty, Status = "Online" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Serial);
    }

    [Fact]
    public void Validate_WhenStatusMissingOrTooLong_ShouldFail()
    {
        UpdateAccessPointStatusRequest missingStatus = new() { Serial = "AP-001", Status = string.Empty };
        UpdateAccessPointStatusRequest tooLongStatus = new() { Serial = "AP-001", Status = new string('x', 51) };

        _validator.TestValidate(missingStatus).ShouldHaveValidationErrorFor(x => x.Status);
        _validator.TestValidate(tooLongStatus).ShouldHaveValidationErrorFor(x => x.Status);
    }
}
