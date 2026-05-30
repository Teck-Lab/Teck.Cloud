// <copyright file="DisplayAssignmentCreatedEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Events;

namespace Device.Domain.Entities.DisplayAssignmentAggregate.Events;

/// <summary>
/// Raised when a new <see cref="DisplayAssignment"/> is created and persisted.
/// </summary>
public sealed class DisplayAssignmentCreatedEvent : DomainEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayAssignmentCreatedEvent"/> class.
    /// </summary>
    /// <param name="assignmentId">The new assignment identifier.</param>
    /// <param name="displayId">The owning display identifier.</param>
    /// <param name="renderJobId">The deterministic render job identifier enqueued for this assignment.</param>
    public DisplayAssignmentCreatedEvent(Guid assignmentId, Guid displayId, Guid renderJobId)
    {
        AssignmentId = assignmentId;
        DisplayId = displayId;
        RenderJobId = renderJobId;
    }

    /// <summary>
    /// Gets the new assignment identifier.
    /// </summary>
    public Guid AssignmentId { get; }

    /// <summary>
    /// Gets the owning display identifier.
    /// </summary>
    public Guid DisplayId { get; }

    /// <summary>
    /// Gets the deterministic render job identifier.
    /// </summary>
    public Guid RenderJobId { get; }
}
