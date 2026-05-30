// <copyright file="RenderConcurrencyLimiter.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace Image.Generator.Application.Render.Features.SubmitRenderJob.V1;

/// <summary>
/// Limits concurrent render work and enforces queue wait timeout semantics.
/// </summary>
public sealed class RenderConcurrencyLimiter : IDisposable
{
    private readonly SemaphoreSlim semaphore;
    private readonly TimeSpan queueTimeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="RenderConcurrencyLimiter"/> class.
    /// </summary>
    /// <param name="options">Render processing configuration options.</param>
    public RenderConcurrencyLimiter(IOptions<RenderProcessingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        RenderProcessingOptions renderOptions = options.Value;
        int configured = renderOptions.MaxConcurrentRenders;

        // Environment.ProcessorCount honors container CPU limits and is safe for Kubernetes pods.
        int computed = configured > 0
            ? configured
            : Math.Max(1, Environment.ProcessorCount);

        this.MaxConcurrentRenders = computed;
        this.queueTimeout = TimeSpan.FromMilliseconds(Math.Max(0, renderOptions.QueueTimeoutMilliseconds));
        this.semaphore = new SemaphoreSlim(computed, computed);
    }

    /// <summary>
    /// Gets the effective maximum number of concurrent render operations.
    /// </summary>
    public int MaxConcurrentRenders { get; }

    /// <summary>
    /// Releases owned resources.
    /// </summary>
    public void Dispose()
    {
        this.semaphore.Dispose();
    }

    internal async ValueTask<RenderConcurrencyLease> TryEnterAsync(CancellationToken cancellationToken)
    {
        Stopwatch waitStopwatch = Stopwatch.StartNew();

        if (this.queueTimeout == TimeSpan.Zero)
        {
            await this.semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return RenderConcurrencyLease.Acquired(this.semaphore, waitStopwatch.Elapsed);
        }

        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(this.queueTimeout);

        try
        {
            await this.semaphore.WaitAsync(timeoutSource.Token).ConfigureAwait(false);
            return RenderConcurrencyLease.Acquired(this.semaphore, waitStopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return RenderConcurrencyLease.TimedOut(waitStopwatch.Elapsed);
        }
    }

    internal readonly struct RenderConcurrencyLease : IDisposable
    {
        private readonly SemaphoreSlim? semaphore;

        private RenderConcurrencyLease(bool acquired, TimeSpan waitDuration, SemaphoreSlim? semaphore)
        {
            this.IsAcquired = acquired;
            this.WaitDuration = waitDuration;
            this.semaphore = semaphore;
        }

        public bool IsAcquired { get; }

        public TimeSpan WaitDuration { get; }

        public static RenderConcurrencyLease Acquired(SemaphoreSlim semaphore, TimeSpan waitDuration)
        {
            return new RenderConcurrencyLease(true, waitDuration, semaphore);
        }

        public static RenderConcurrencyLease TimedOut(TimeSpan waitDuration)
        {
            return new RenderConcurrencyLease(false, waitDuration, semaphore: null);
        }

        public void Dispose()
        {
            if (this.IsAcquired)
            {
                this.semaphore?.Release();
            }
        }
    }
}
