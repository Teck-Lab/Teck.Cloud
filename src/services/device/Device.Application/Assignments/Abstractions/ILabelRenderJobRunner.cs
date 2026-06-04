// <copyright file="ILabelRenderJobRunner.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.Assignments.Abstractions;

/// <summary>
/// Enqueues and tracks label render jobs for display assignments.
/// </summary>
public interface ILabelRenderJobRunner
{
    /// <summary>
    /// Enqueues a label render job for the specified display and zone payload.
    /// </summary>
    /// <param name="jobId">The deterministic render job identifier.</param>
    /// <param name="displayId">The display identifier.</param>
    /// <param name="layoutName">The layout or template name used for rendering.</param>
    /// <param name="zones">The zone payload to render.</param>
    /// <param name="templateDesign">The optional resolved template design snapshot.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The enqueue result with job identifier and initial status.</returns>
    ValueTask<LabelRenderJobResult> EnqueueAsync(
        Guid jobId,
        Guid displayId,
        string layoutName,
        IReadOnlyCollection<LabelRenderJobZoneItem> zones,
        ResolvedTemplateDesignSnapshot? templateDesign,
        CancellationToken cancellationToken);
}

/// <summary>
/// Zone payload item for label rendering.
/// </summary>
/// <param name="ZoneIndex">The zone index in the target layout.</param>
/// <param name="ProductId">The product identifier to render in the zone.</param>
public sealed record LabelRenderJobZoneItem(int ZoneIndex, Guid ProductId);

/// <summary>
/// Result returned after render job enqueue.
/// </summary>
/// <param name="JobId">The render job identifier.</param>
/// <param name="Status">The initial render job status.</param>
public sealed record LabelRenderJobResult(Guid JobId, string Status);
