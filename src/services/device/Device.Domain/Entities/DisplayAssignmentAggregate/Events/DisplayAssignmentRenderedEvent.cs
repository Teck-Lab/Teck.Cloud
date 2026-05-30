// <copyright file="DisplayAssignmentRenderedEvent.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Events;

namespace Device.Domain.Entities.DisplayAssignmentAggregate.Events;

/// <summary>
/// Raised when the image-generator reports a completed render for this assignment.
/// </summary>
public sealed class DisplayAssignmentRenderedEvent : DomainEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayAssignmentRenderedEvent"/> class.
    /// </summary>
    /// <param name="assignmentId">The assignment identifier.</param>
    /// <param name="displayId">The owning display identifier.</param>
    /// <param name="renderedImageUri">The location of the rendered image (blob URI or local path).</param>
    public DisplayAssignmentRenderedEvent(Guid assignmentId, Guid displayId, Uri renderedImageUri)
    {
        ArgumentNullException.ThrowIfNull(renderedImageUri);

        AssignmentId = assignmentId;
        DisplayId = displayId;
        RenderedImageUri = renderedImageUri;
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
    /// Gets the location of the rendered image.
    /// </summary>
    public Uri RenderedImageUri { get; }
}
