// <copyright file="DisplayAssignmentTests.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.Entities.DisplayAssignmentAggregate;
using Device.Domain.Entities.DisplayAssignmentAggregate.Events;
using ErrorOr;
using Shouldly;

namespace Device.UnitTests.Domain.Entities;

public sealed class DisplayAssignmentTests
{
    [Fact]
    public void Create_WhenCalled_ShouldEmitCreatedDomainEvent()
    {
        ErrorOr<DisplayAssignment> result = DisplayAssignment.Create(Guid.NewGuid(), "zone-a", "template-1", "Request", null, null, [new DisplayAssignmentZone(1, Guid.NewGuid())]);
        result.IsError.ShouldBeFalse();
        result.Value.DomainEvents.ShouldContain(x => x is DisplayAssignmentCreatedEvent);
    }

    [Fact]
    public void MarkRendered_WhenPending_ShouldEmitRenderedDomainEvent()
    {
        DisplayAssignment assignment = DisplayAssignment.Create(Guid.NewGuid(), "zone-a", "template-1", "Request", null, null, [new DisplayAssignmentZone(1, Guid.NewGuid())]).Value;
        ErrorOr<Success> result = assignment.MarkRendered(new Uri("https://cdn.test/rendered.png"), DateTimeOffset.UtcNow);
        result.IsError.ShouldBeFalse();
        assignment.DomainEvents.ShouldContain(x => x is DisplayAssignmentRenderedEvent);
    }

    [Fact]
    public void MarkDelivered_WhenNotRendered_ShouldReturnConflict()
    {
        DisplayAssignment assignment = DisplayAssignment.Create(Guid.NewGuid(), "zone-a", "template-1", "Request", null, null, [new DisplayAssignmentZone(1, Guid.NewGuid())]).Value;
        ErrorOr<Success> result = assignment.MarkDelivered(DateTimeOffset.UtcNow);
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("DisplayAssignment.InvalidTransitionToDelivered");
    }

    [Fact]
    public void MarkFailed_WhenFailureReasonMissing_ShouldReturnValidationError()
    {
        DisplayAssignment assignment = DisplayAssignment.Create(Guid.NewGuid(), "zone-a", "template-1", "Request", null, null, [new DisplayAssignmentZone(1, Guid.NewGuid())]).Value;
        ErrorOr<Success> result = assignment.MarkFailed(string.Empty);
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("DisplayAssignment.FailureReasonRequired");
    }
}
