// <copyright file="DisplayAssignmentFailedEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Events;

namespace Device.Domain.Entities.DisplayAssignmentAggregate.Events;

/// <summary>
/// Raised when an assignment terminates in a failure state (render or vendor delivery).
/// </summary>
public sealed class DisplayAssignmentFailedEvent : DomainEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayAssignmentFailedEvent"/> class.
    /// </summary>
    /// <param name="assignmentId">The assignment identifier.</param>
    /// <param name="displayId">The owning display identifier.</param>
    /// <param name="failureReason">Short failure reason for diagnostics.</param>
    public DisplayAssignmentFailedEvent(Guid assignmentId, Guid displayId, string failureReason)
    {
        AssignmentId = assignmentId;
        DisplayId = displayId;
        FailureReason = failureReason;
    }

    /// <summary>
    /// Gets the assignment identifier.
    /// </summary>
    public Guid AssignmentId { get; }

    /// <summary>
    /// Gets the owning display identifier.
    /// </summary>
    public Guid DisplayId { get; }

    /// <summary>
    /// Gets the short failure reason.
    /// </summary>
    public string FailureReason { get; }
}
