// <copyright file="DisplayAssignmentSnapshotTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DisplayAssignmentAggregate;
using ErrorOr;
using Shouldly;

namespace Device.UnitTests.Domain.Entities;

public class DisplayAssignmentSnapshotTests
{
    private static readonly Guid TestDisplayId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string TestLocationNodeId = "location-1";
    private const string TestTemplateId = "template-abc";
    private const string TestTemplateSource = "Inherited";

    [Fact]
    public void Create_WithSnapshots_ShouldCaptureTemplateAndProductData()
    {
        // Arrange
        string templateSnapshot = """{"width":100,"height":200,"elements":[]}""";
        string productDataSnapshot = """[{"productId":"p1","name":"Test"}]""";
        DisplayAssignmentZone[] zones = [new DisplayAssignmentZone(0, Guid.NewGuid())];

        // Act
        ErrorOr<DisplayAssignment> result = DisplayAssignment.Create(
            TestDisplayId,
            TestLocationNodeId,
            TestTemplateId,
            TestTemplateSource,
            templateSnapshot,
            productDataSnapshot,
            zones);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.TemplateSnapshot.ShouldBe(templateSnapshot);
        result.Value.ProductDataSnapshot.ShouldBe(productDataSnapshot);
    }

    [Fact]
    public void Create_WithNullSnapshots_ShouldAllowNullValues()
    {
        // Arrange
        DisplayAssignmentZone[] zones = [new DisplayAssignmentZone(0, Guid.NewGuid())];

        // Act
        ErrorOr<DisplayAssignment> result = DisplayAssignment.Create(
            TestDisplayId,
            TestLocationNodeId,
            TestTemplateId,
            TestTemplateSource,
            null,
            null,
            zones);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.TemplateSnapshot.ShouldBeNull();
        result.Value.ProductDataSnapshot.ShouldBeNull();
    }

    [Fact]
    public void Create_WithEmptySnapshots_ShouldAllowEmptyStrings()
    {
        // Arrange
        DisplayAssignmentZone[] zones = [new DisplayAssignmentZone(0, Guid.NewGuid())];

        // Act
        ErrorOr<DisplayAssignment> result = DisplayAssignment.Create(
            TestDisplayId,
            TestLocationNodeId,
            TestTemplateId,
            TestTemplateSource,
            string.Empty,
            string.Empty,
            zones);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.TemplateSnapshot.ShouldBe(string.Empty);
        result.Value.ProductDataSnapshot.ShouldBe(string.Empty);
    }

    [Fact]
    public void Create_WithLargeSnapshot_ShouldStoreValue()
    {
        // Arrange
        string largeSnapshot = new string('x', 8000);
        DisplayAssignmentZone[] zones = [new DisplayAssignmentZone(0, Guid.NewGuid())];

        // Act
        ErrorOr<DisplayAssignment> result = DisplayAssignment.Create(
            TestDisplayId,
            TestLocationNodeId,
            TestTemplateId,
            TestTemplateSource,
            largeSnapshot,
            largeSnapshot,
            zones);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.TemplateSnapshot.ShouldBe(largeSnapshot);
        result.Value.ProductDataSnapshot.ShouldBe(largeSnapshot);
    }

    [Fact]
    public void Create_WithoutSnapshots_ShouldSetStatusPending()
    {
        // Arrange
        DisplayAssignmentZone[] zones = [new DisplayAssignmentZone(0, Guid.NewGuid())];

        // Act
        ErrorOr<DisplayAssignment> result = DisplayAssignment.Create(
            TestDisplayId,
            TestLocationNodeId,
            TestTemplateId,
            TestTemplateSource,
            null,
            null,
            zones);

        // Assert
        result.Value.Status.ShouldBe(DisplayAssignmentStatus.Pending);
        result.Value.RenderedImageUri.ShouldBeNull();
        result.Value.RenderedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void MarkRendered_ShouldSetImageUriAndStatus()
    {
        // Arrange
        DisplayAssignmentZone[] zones = [new DisplayAssignmentZone(0, Guid.NewGuid())];
        ErrorOr<DisplayAssignment> created = DisplayAssignment.Create(
            TestDisplayId,
            TestLocationNodeId,
            TestTemplateId,
            TestTemplateSource,
            null,
            null,
            zones);

        Uri imageUri = new("http://storage/teck-images/test.png");
        DateTimeOffset renderedAt = DateTimeOffset.UtcNow;

        // Act
        ErrorOr<Success> result = created.Value.MarkRendered(imageUri, renderedAt);

        // Assert
        result.IsError.ShouldBeFalse();
        created.Value.Status.ShouldBe(DisplayAssignmentStatus.Rendered);
        created.Value.RenderedImageUri.ShouldBe(imageUri);
        created.Value.RenderedAtUtc.ShouldBe(renderedAt);
    }

    [Fact]
    public void MarkRendered_WhenNotPending_ShouldReturnError()
    {
        // Arrange
        DisplayAssignmentZone[] zones = [new DisplayAssignmentZone(0, Guid.NewGuid())];
        ErrorOr<DisplayAssignment> created = DisplayAssignment.Create(
            TestDisplayId,
            TestLocationNodeId,
            TestTemplateId,
            TestTemplateSource,
            null,
            null,
            zones);

        created.Value.MarkRendered(new Uri("http://test.png"), DateTimeOffset.UtcNow);

        // Act
        ErrorOr<Success> result = created.Value.MarkRendered(new Uri("http://test2.png"), DateTimeOffset.UtcNow);

        // Assert
        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithEmptyDisplayId_ShouldReturnValidationError()
    {
        // Arrange
        DisplayAssignmentZone[] zones = [new DisplayAssignmentZone(0, Guid.NewGuid())];

        // Act
        ErrorOr<DisplayAssignment> result = DisplayAssignment.Create(
            Guid.Empty,
            TestLocationNodeId,
            TestTemplateId,
            TestTemplateSource,
            null,
            null,
            zones);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors[0].Code.ShouldBe("DisplayAssignment.DisplayIdRequired");
    }

    [Fact]
    public void Create_WithEmptyLocationNodeId_ShouldReturnValidationError()
    {
        // Arrange
        DisplayAssignmentZone[] zones = [new DisplayAssignmentZone(0, Guid.NewGuid())];

        // Act
        ErrorOr<DisplayAssignment> result = DisplayAssignment.Create(
            TestDisplayId,
            string.Empty,
            TestTemplateId,
            TestTemplateSource,
            null,
            null,
            zones);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors[0].Code.ShouldBe("DisplayAssignment.LocationNodeIdRequired");
    }

    [Fact]
    public void Create_WithEmptyTemplateId_ShouldReturnValidationError()
    {
        // Arrange
        DisplayAssignmentZone[] zones = [new DisplayAssignmentZone(0, Guid.NewGuid())];

        // Act
        ErrorOr<DisplayAssignment> result = DisplayAssignment.Create(
            TestDisplayId,
            TestLocationNodeId,
            string.Empty,
            TestTemplateSource,
            null,
            null,
            zones);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors[0].Code.ShouldBe("DisplayAssignment.TemplateRequired");
    }

    [Fact]
    public void Create_WithEmptyZones_ShouldReturnValidationError()
    {
        // Act
        ErrorOr<DisplayAssignment> result = DisplayAssignment.Create(
            TestDisplayId,
            TestLocationNodeId,
            TestTemplateId,
            TestTemplateSource,
            null,
            null,
            Array.Empty<DisplayAssignmentZone>());

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors[0].Code.ShouldBe("DisplayAssignment.ZonesRequired");
    }

    [Fact]
    public void MarkDelivered_ShouldTransitionFromRendered()
    {
        // Arrange
        DisplayAssignmentZone[] zones = [new DisplayAssignmentZone(0, Guid.NewGuid())];
        ErrorOr<DisplayAssignment> created = DisplayAssignment.Create(
            TestDisplayId,
            TestLocationNodeId,
            TestTemplateId,
            TestTemplateSource,
            null,
            null,
            zones);

        created.Value.MarkRendered(new Uri("http://test.png"), DateTimeOffset.UtcNow);
        DateTimeOffset deliveredAt = DateTimeOffset.UtcNow;

        // Act
        ErrorOr<Success> result = created.Value.MarkDelivered(deliveredAt);

        // Assert
        result.IsError.ShouldBeFalse();
        created.Value.Status.ShouldBe(DisplayAssignmentStatus.Delivered);
        created.Value.DeliveredAtUtc.ShouldBe(deliveredAt);
    }

    [Fact]
    public void MarkFailed_ShouldSetFailureReasonAndStatus()
    {
        // Arrange
        DisplayAssignmentZone[] zones = [new DisplayAssignmentZone(0, Guid.NewGuid())];
        ErrorOr<DisplayAssignment> created = DisplayAssignment.Create(
            TestDisplayId,
            TestLocationNodeId,
            TestTemplateId,
            TestTemplateSource,
            null,
            null,
            zones);

        // Act
        ErrorOr<Success> result = created.Value.MarkFailed("Rendering timeout");

        // Assert
        result.IsError.ShouldBeFalse();
        created.Value.Status.ShouldBe(DisplayAssignmentStatus.Failed);
        created.Value.FailureReason.ShouldBe("Rendering timeout");
    }
}
