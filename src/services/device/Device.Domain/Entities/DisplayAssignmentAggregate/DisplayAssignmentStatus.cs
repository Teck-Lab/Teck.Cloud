// <copyright file="DisplayAssignmentStatus.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Domain.Entities.DisplayAssignmentAggregate;

/// <summary>
/// Lifecycle states for a <see cref="DisplayAssignment"/>.
/// </summary>
public enum DisplayAssignmentStatus
{
    /// <summary>
    /// Persisted, render job enqueued, awaiting render completion.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Render completed; image is available for vendor dispatch.
    /// </summary>
    Rendered = 1,

    /// <summary>
    /// Vendor device server has acknowledged delivery to the physical ESL.
    /// </summary>
    Delivered = 2,

    /// <summary>
    /// Render or delivery failed. <see cref="DisplayAssignment.FailureReason"/> describes why.
    /// </summary>
    Failed = 3,
}
