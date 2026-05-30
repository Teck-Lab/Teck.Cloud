// <copyright file="DisplayAssignmentDeliveredEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Events;

namespace Device.Domain.Entities.DisplayAssignmentAggregate.Events;

/// <summary>
/// Raised when a vendor device server acknowledges delivery of the rendered image to the physical ESL.
/// </summary>
public sealed class DisplayAssignmentDeliveredEvent : DomainEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayAssignmentDeliveredEvent"/> class.
    /// </summary>
    /// <param name="assignmentId">The assignment identifier.</param>
    /// <param name="displayId">The owning display identifier.</param>
    public DisplayAssignmentDeliveredEvent(Guid assignmentId, Guid displayId)
    {
        AssignmentId = assignmentId;
        DisplayId = displayId;
    }

    /// <summary>
    /// Gets the assignment identifier.
    /// </summary>
    public Guid AssignmentId { get; }

    /// <summary>
    /// Gets the owning display identifier.
    /// </summary>
    public Guid DisplayId { get; }
}
