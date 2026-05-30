// <copyright file="SubmitRenderJobResponse.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Image.Generator.Application.Render.Features.SubmitRenderJob.V1;

/// <summary>
/// Represents the response returned after a render job submission.
/// </summary>
public sealed record SubmitRenderJobResponse
{
    /// <summary>
    /// Gets the unique identifier of the render job.
    /// </summary>
    public Guid JobId { get; init; }

    /// <summary>
    /// Gets the current render job status.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Gets the generated image URI when available.
    /// </summary>
    public Uri? ImageUri { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the job was queued.
    /// </summary>
    public DateTimeOffset QueuedAtUtc { get; init; }
}
