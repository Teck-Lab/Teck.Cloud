// <copyright file="IDisplayAssignmentReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.Assignments.Abstractions;

/// <summary>
/// Read-side repository for display-assignment queries used outside the command path
/// (status lookups, vendor-dispatch consumers, projections).
/// </summary>
public interface IDisplayAssignmentReadRepository
{
    /// <summary>
    /// Gets a flattened summary of an assignment for read consumers.
    /// </summary>
    /// <param name="assignmentId">The assignment identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The summary, or null when no such assignment exists.</returns>
    ValueTask<DisplayAssignmentSummary?> GetSummaryByIdAsync(Guid assignmentId, CancellationToken cancellationToken);
}

/// <summary>
/// Read-model projection of <see cref="Device.Domain.Entities.DisplayAssignmentAggregate.DisplayAssignment"/>
/// returned by <see cref="IDisplayAssignmentReadRepository"/>.
/// </summary>
/// <param name="AssignmentId">The assignment identifier.</param>
/// <param name="DisplayId">The display this assignment is bound to.</param>
/// <param name="ResolvedTemplateId">The resolved template used for rendering.</param>
/// <param name="Status">The current lifecycle status (mirrors <see cref="Device.Domain.Entities.DisplayAssignmentAggregate.DisplayAssignmentStatus"/>).</param>
/// <param name="RenderJobId">The deterministic render-job identifier.</param>
/// <param name="RenderedImageUri">The rendered image URI once available.</param>
public sealed record DisplayAssignmentSummary(
    Guid AssignmentId,
    Guid DisplayId,
    string ResolvedTemplateId,
    string Status,
    Guid RenderJobId,
    Uri? RenderedImageUri);
