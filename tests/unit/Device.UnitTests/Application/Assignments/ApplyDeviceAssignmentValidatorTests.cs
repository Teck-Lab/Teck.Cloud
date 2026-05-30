// <copyright file="ApplyDeviceAssignmentValidatorTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.Features.ApplyDeviceAssignment.V1;
using FluentValidation.TestHelper;

namespace Device.UnitTests.Application.Assignments;

public sealed class ApplyDeviceAssignmentValidatorTests
{
    private readonly ApplyDeviceAssignmentValidator _validator = new();

    private static ApplyDeviceAssignmentRequest CreateRequest(
        string deviceId = "disp-001",
        string locationNodeId = "shelf-a1",
        IReadOnlyList<ApplyDeviceAssignmentZoneRequest>? zones = null) => new()
    {
        DeviceId = deviceId,
        LocationNodeId = locationNodeId,
        Zones = zones ??
        [
            new ApplyDeviceAssignmentZoneRequest { ZoneIndex = 1, ProductId = "prod-001" },
            new ApplyDeviceAssignmentZoneRequest { ZoneIndex = 2, ProductId = "prod-002" },
        ],
    };

    [Fact]
    public void Validate_ShouldHaveNoErrors_WhenRequestIsValid()
    {
        // Act
        var result = _validator.TestValidate(CreateRequest());

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenDeviceIdIsEmpty()
    {
        // Act
        var result = _validator.TestValidate(CreateRequest(deviceId: string.Empty));

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.DeviceId);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenLocationNodeIdIsEmpty()
    {
        // Act
        var result = _validator.TestValidate(CreateRequest(locationNodeId: string.Empty));

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.LocationNodeId);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenZonesIsNull()
    {
        // Arrange
        var request = new ApplyDeviceAssignmentRequest
        {
            DeviceId = "disp-001",
            LocationNodeId = "shelf-a1",
            Zones = null!,
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Zones);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenZonesIsEmpty()
    {
        // Act
        var result = _validator.TestValidate(CreateRequest(zones: []));

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Zones);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenZonesExceedsMaxCount()
    {
        // Arrange
        var zones = new List<ApplyDeviceAssignmentZoneRequest>
        {
            new() { ZoneIndex = 1, ProductId = "p1" },
            new() { ZoneIndex = 2, ProductId = "p2" },
            new() { ZoneIndex = 3, ProductId = "p3" },
            new() { ZoneIndex = 4, ProductId = "p4" },
        };

        // Act
        var result = _validator.TestValidate(CreateRequest(zones: zones));

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Zones);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenZoneIndexesAreNotUnique()
    {
        // Arrange
        var zones = new List<ApplyDeviceAssignmentZoneRequest>
        {
            new() { ZoneIndex = 1, ProductId = "p1" },
            new() { ZoneIndex = 1, ProductId = "p2" },
        };

        // Act
        var result = _validator.TestValidate(CreateRequest(zones: zones));

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Zones);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(100)]
    public void Validate_ShouldHaveError_WhenZoneIndexIsOutOfRange(int zoneIndex)
    {
        // Arrange
        var zones = new List<ApplyDeviceAssignmentZoneRequest>
        {
            new() { ZoneIndex = zoneIndex, ProductId = "prod-001" },
        };

        // Act
        var result = _validator.TestValidate(CreateRequest(zones: zones));

        // Assert
        result.ShouldHaveValidationErrorFor("Zones[0].ZoneIndex");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Validate_ShouldHaveNoError_WhenZoneIndexIsInRange(int zoneIndex)
    {
        // Arrange
        var zones = new List<ApplyDeviceAssignmentZoneRequest>
        {
            new() { ZoneIndex = zoneIndex, ProductId = "prod-001" },
        };

        // Act
        var result = _validator.TestValidate(CreateRequest(zones: zones));

        // Assert
        result.ShouldNotHaveValidationErrorFor("Zones[0].ZoneIndex");
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenProductIdIsEmpty()
    {
        // Arrange
        var zones = new List<ApplyDeviceAssignmentZoneRequest>
        {
            new() { ZoneIndex = 1, ProductId = string.Empty },
        };

        // Act
        var result = _validator.TestValidate(CreateRequest(zones: zones));

        // Assert
        result.ShouldHaveValidationErrorFor("Zones[0].ProductId");
    }

    [Fact]
    public void Validate_ShouldHaveMultipleErrors_WhenMultipleFieldsAreInvalid()
    {
        // Arrange
        var zones = new List<ApplyDeviceAssignmentZoneRequest>();

        // Act
        var result = _validator.TestValidate(CreateRequest(
            deviceId: string.Empty,
            locationNodeId: string.Empty,
            zones: zones));

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.DeviceId);
        result.ShouldHaveValidationErrorFor(r => r.LocationNodeId);
        result.ShouldHaveValidationErrorFor(r => r.Zones);
    }
}
