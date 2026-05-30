// <copyright file="RenderProcessingOptions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Image.Generator.Application.Render.Features.SubmitRenderJob.V1;

/// <summary>
/// Configuration options for render throughput, validation limits, and cache behavior.
/// </summary>
public sealed class RenderProcessingOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string Section = "RenderProcessing";

    /// <summary>
    /// Gets the maximum concurrent render operations. Values less than or equal to zero use CPU-based defaults.
    /// </summary>
    public int MaxConcurrentRenders { get; init; }

    /// <summary>
    /// Gets the maximum time to wait in the render queue before returning a busy response.
    /// </summary>
    public int QueueTimeoutMilliseconds { get; init; } = 15000;

    /// <summary>
    /// Gets the maximum allowed template canvas area in pixels.
    /// </summary>
    public int MaxCanvasPixels { get; init; } = 16777216;

    /// <summary>
    /// Gets the maximum number of template elements per render request.
    /// </summary>
    public int MaxTemplateElements { get; init; } = 512;

    /// <summary>
    /// Gets a value indicating whether missing bindings should fail rendering.
    /// </summary>
    public bool StrictBindings { get; init; }

    /// <summary>
    /// Gets a value indicating whether rendered payloads should be cached.
    /// </summary>
    public bool EnableRenderCache { get; init; } = true;

    /// <summary>
    /// Gets the cache duration in seconds for rendered payloads.
    /// </summary>
    public int RenderCacheDurationSeconds { get; init; } = 300;

    /// <summary>
    /// Gets the maximum payload size in bytes allowed to be stored in cache.
    /// </summary>
    public int MaxCachePayloadBytes { get; init; } = 3145728;

    /// <summary>
    /// Gets the pixel stride used to check cancellation during palette remapping.
    /// </summary>
    public int PaletteCancellationCheckStridePixels { get; init; } = 4096;
}
