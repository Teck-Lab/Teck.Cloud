// <copyright file="AccessPointTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.AccessPoints;
using ErrorOr;
using Shouldly;

namespace Device.UnitTests.Domain.AccessPoints;

public sealed class AccessPointTests
{
    [Fact]
    public void Create_WhenSerialNumberIsEmpty_ShouldReturnValidationError()
    {
        // Act
        ErrorOr<AccessPoint> result = AccessPoint.Create(string.Empty, "Hanshow", "shelf-a1", 10);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Code.ShouldBe("AccessPoint.SerialNumberRequired");
    }

    [Fact]
    public void Create_WhenVendorIsEmpty_ShouldReturnValidationError()
    {
        // Act
        ErrorOr<AccessPoint> result = AccessPoint.Create("AP-001", string.Empty, "shelf-a1", 10);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Code.ShouldBe("AccessPoint.VendorRequired");
    }

    [Fact]
    public void Create_WhenLocationNodeIdIsEmpty_ShouldReturnValidationError()
    {
        // Act
        ErrorOr<AccessPoint> result = AccessPoint.Create("AP-001", "Hanshow", string.Empty, 10);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Code.ShouldBe("AccessPoint.LocationNodeIdRequired");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WhenMaxCapacityIsInvalid_ShouldReturnValidationError(int maxCapacity)
    {
        // Act
        ErrorOr<AccessPoint> result = AccessPoint.Create("AP-001", "Hanshow", "shelf-a1", maxCapacity);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Code.ShouldBe("AccessPoint.MaxCapacityInvalid");
    }

    [Fact]
    public void Create_WhenParametersAreValid_ShouldReturnAccessPointWithNormalisedFields()
    {
        // Act
        ErrorOr<AccessPoint> result = AccessPoint.Create("  ap-001  ", "  Hanshow  ", "  shelf-a1  ", 10);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.SerialNumber.ShouldBe("AP-001");
        result.Value.Vendor.ShouldBe("Hanshow");
        result.Value.LocationNodeId.ShouldBe("shelf-a1");
        result.Value.Status.ShouldBe(AccessPointStatus.Online);
        result.Value.CurrentLoad.ShouldBe(0);
        result.Value.MaxCapacity.ShouldBe(10);
    }

    [Fact]
    public void IncrementLoad_WhenBelowCapacity_ShouldIncrementCurrentLoad()
    {
        // Arrange
        AccessPoint accessPoint = AccessPoint.Create("AP-001", "Hanshow", "shelf-a1", 2).Value;

        // Act
        ErrorOr<Success> result = accessPoint.IncrementLoad();

        // Assert
        result.IsError.ShouldBeFalse();
        accessPoint.CurrentLoad.ShouldBe(1);
    }

    [Fact]
    public void IncrementLoad_WhenAtCapacity_ShouldReturnConflictError()
    {
        // Arrange
        AccessPoint accessPoint = AccessPoint.Create("AP-001", "Hanshow", "shelf-a1", 1).Value;
        accessPoint.IncrementLoad();

        // Act
        ErrorOr<Success> result = accessPoint.IncrementLoad();

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe("AccessPoint.CapacityExceeded");
        accessPoint.CurrentLoad.ShouldBe(1);
    }

    [Fact]
    public void IncrementLoad_WhenCalledAgainAtCapacity_ShouldRemainAtMaxCapacity()
    {
        // Arrange
        AccessPoint accessPoint = AccessPoint.Create("AP-001", "Hanshow", "shelf-a1", 1).Value;
        accessPoint.IncrementLoad();

        // Act
        ErrorOr<Success> first = accessPoint.IncrementLoad();
        ErrorOr<Success> second = accessPoint.IncrementLoad();

        // Assert
        first.IsError.ShouldBeTrue();
        second.IsError.ShouldBeTrue();
        accessPoint.CurrentLoad.ShouldBe(1);
    }

    [Fact]
    public void DecrementLoad_WhenCurrentLoadIsGreaterThanZero_ShouldDecrementCurrentLoad()
    {
        // Arrange
        AccessPoint accessPoint = AccessPoint.Create("AP-001", "Hanshow", "shelf-a1", 5).Value;
        accessPoint.IncrementLoad();

        // Act
        accessPoint.DecrementLoad();

        // Assert
        accessPoint.CurrentLoad.ShouldBe(0);
    }

    [Fact]
    public void DecrementLoad_WhenCurrentLoadIsZero_ShouldDoNothing()
    {
        // Arrange
        AccessPoint accessPoint = AccessPoint.Create("AP-001", "Hanshow", "shelf-a1", 5).Value;

        // Act
        accessPoint.DecrementLoad();

        // Assert
        accessPoint.CurrentLoad.ShouldBe(0);
    }

    [Fact]
    public void SetStatus_WhenCalled_ShouldChangeStatus()
    {
        // Arrange
        AccessPoint accessPoint = AccessPoint.Create("AP-001", "Hanshow", "shelf-a1", 5).Value;

        // Act
        accessPoint.SetStatus(AccessPointStatus.Offline);

        // Assert
        accessPoint.Status.ShouldBe(AccessPointStatus.Offline);
    }
}
