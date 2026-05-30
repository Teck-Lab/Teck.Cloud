// <copyright file="SubmitBatchRenderJob.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Security.Cryptography;
using ErrorOr;
using Image.Generator.Application.Render.Features.SubmitRenderJob.V1;
using Image.Generator.Application.Storage;
using Microsoft.Extensions.Options;
using SharedKernel.Core.CQRS;
using SharedKernel.Events;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;

namespace Image.Generator.Application.Render.Features.SubmitBatchRenderJob.V1;

/// <summary>
/// Represents a batch request to render multiple images using a shared template.
/// </summary>
public sealed record BatchRenderItem(Guid DisplayId, IDictionary<string, string> Data);

/// <summary>
/// Batch render command that shares a single template across many display/data combinations.
/// </summary>
public sealed record SubmitBatchRenderJobCommand(
    Guid BatchId,
    string TenantId,
    string OutputType,
    IReadOnlyList<string> PaletteColors,
    SubmitRenderJobTemplateRequest Template,
    IReadOnlyList<BatchRenderItem> Items)
    : ICommand<ErrorOr<SubmitBatchRenderJobResponse>>;

/// <summary>
/// Response containing all rendered image URIs from the batch.
/// </summary>
public sealed record SubmitBatchRenderJobResponse
{
    /// <summary>
    /// Gets the unique batch identifier.
    /// </summary>
    public required Guid BatchId { get; init; }

    /// <summary>
    /// Gets the list of individual render results.
    /// </summary>
    public required IReadOnlyList<BatchRenderResult> Results { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the batch completed.
    /// </summary>
    public required DateTimeOffset CompletedAtUtc { get; init; }
}

/// <summary>
/// Individual result from a batch render operation.
/// </summary>
public sealed record BatchRenderResult
{
    /// <summary>
    /// Gets the display identifier for the rendered item.
    /// </summary>
    public required Guid DisplayId { get; init; }

    /// <summary>
    /// Gets the render status.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets the rendered image URI, or null if rendering failed.
    /// </summary>
    public Uri? ImageUri { get; init; }

    /// <summary>
    /// Gets the error code if rendering failed, otherwise null.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Gets the error message if rendering failed, otherwise null.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Handles batch render requests with optimized shared template compilation and parallel execution.
/// </summary>
public sealed class SubmitBatchRenderJobCommandHandler(
    IOptions<RenderProcessingOptions> renderOptions,
    ITenantFontAssetStore tenantFontAssetStore,
    IFusionCache fusionCache,
    IMessageBus messageBus,
    IImageStorage imageStorage,
    ILogger<SubmitBatchRenderJobCommandHandler> logger)
    : ICommandHandler<SubmitBatchRenderJobCommand, ErrorOr<SubmitBatchRenderJobResponse>>
{
    private static readonly Meter BatchMeter = new("Teck.Cloud.ImageGenerator.Batch");
    private static readonly Counter<long> BatchRequestCounter = BatchMeter.CreateCounter<long>("image_generator.batch.requests");
    private static readonly Counter<long> BatchItemCounter = BatchMeter.CreateCounter<long>("image_generator.batch.items");
    private static readonly Counter<long> BatchFailureCounter = BatchMeter.CreateCounter<long>("image_generator.batch.failures");
    private static readonly Histogram<double> BatchDurationMilliseconds = BatchMeter.CreateHistogram<double>("image_generator.batch.duration_ms");
    private static readonly Histogram<double> BatchItemDurationMilliseconds = BatchMeter.CreateHistogram<double>("image_generator.batch.item_duration_ms");

    private readonly RenderProcessingOptions renderOptions = renderOptions.Value;
    private readonly ITenantFontAssetStore tenantFontAssetStore = tenantFontAssetStore;
    private readonly IFusionCache fusionCache = fusionCache;
    private readonly IMessageBus messageBus = messageBus;
    private readonly IImageStorage imageStorage = imageStorage;
    private readonly ILogger<SubmitBatchRenderJobCommandHandler> logger = logger;

    /// <summary>
    /// Handles the batch render command.
    /// </summary>
    /// <param name="request">The batch render request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The batch render response or an error.</returns>
    public async ValueTask<ErrorOr<SubmitBatchRenderJobResponse>> Handle(
        SubmitBatchRenderJobCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Items.Count == 0)
        {
            return Error.Validation("ImageGenerator.Batch.Empty", "Batch render request must contain at least one item.");
        }

        Stopwatch totalStopwatch = Stopwatch.StartNew();
        BatchRequestCounter.Add(1);
        BatchItemCounter.Add(request.Items.Count);

        Guid batchId = request.BatchId != Guid.Empty ? request.BatchId : Guid.NewGuid();
        string outputType = request.OutputType.Trim().ToLowerInvariant();

        // Pre-compile template once for all items
        CompiledTemplate compiledTemplate = TemplateCompiler.Compile(request.Template);

        // Pre-fetch tenant fonts once
        IReadOnlyDictionary<string, string> tenantFontPaths = await this.tenantFontAssetStore
            .EnsureFontsAvailableAsync(
                request.TenantId,
                request.Template.Elements.Select(static element => element.FontFamily).ToArray(),
                cancellationToken)
            .ConfigureAwait(false);

        RenderExecutionSettings executionSettings = new(
            this.renderOptions.StrictBindings,
            this.renderOptions.PaletteCancellationCheckStridePixels,
            tenantFontPaths);

        // Process items in parallel with throttling
        List<BatchRenderResult> results = new(request.Items.Count);
        using SemaphoreSlim throttle = new(this.renderOptions.MaxConcurrentRenders, this.renderOptions.MaxConcurrentRenders);

        await Parallel.ForEachAsync(
            request.Items,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = this.renderOptions.MaxConcurrentRenders,
                CancellationToken = cancellationToken,
            },
            async (item, ct) =>
            {
                await throttle.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    Stopwatch itemStopwatch = Stopwatch.StartNew();
                    BatchRenderResult result = await RenderSingleItemAsync(
                        batchId,
                        request.TenantId,
                        outputType,
                        request.PaletteColors,
                        compiledTemplate,
                        executionSettings,
                        item,
                        ct).ConfigureAwait(false);

                    BatchItemDurationMilliseconds.Record(itemStopwatch.Elapsed.TotalMilliseconds);

                    lock (results)
                    {
                        results.Add(result);
                    }
                }
                finally
                {
                    throttle.Release();
                }
            });

        BatchDurationMilliseconds.Record(totalStopwatch.Elapsed.TotalMilliseconds);

        if (this.logger.IsEnabled(LogLevel.Information))
        {
            int successCount = results.Count(renderResult => renderResult.Status is "rendered" or "rendered-cache");
            int failedCount = results.Count(renderResult => renderResult.Status == "failed");

            this.logger.LogInformation(
                "Batch render completed. BatchId={BatchId} TotalItems={TotalItems} Success={SuccessCount} Failed={FailedCount} DurationMs={DurationMs}",
                batchId,
                request.Items.Count,
                successCount,
                failedCount,
                totalStopwatch.Elapsed.TotalMilliseconds);
        }

        return new SubmitBatchRenderJobResponse
        {
            BatchId = batchId,
            Results = results.OrderBy(renderResult => renderResult.DisplayId).ToList(),
            CompletedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private async Task<BatchRenderResult> RenderSingleItemAsync(
        Guid batchId,
        string tenantId,
        string outputType,
        IReadOnlyList<string> paletteColors,
        CompiledTemplate compiledTemplate,
        RenderExecutionSettings executionSettings,
        BatchRenderItem item,
        CancellationToken cancellationToken)
    {
        Guid jobId = Guid.NewGuid();

        try
        {
            // Build per-item command for cache key and renderer compatibility
            SubmitRenderJobCommand singleCommand = new(
                jobId,
                item.DisplayId,
                tenantId,
                outputType,
                paletteColors,
                compiledTemplate.OriginalTemplate,
                (IReadOnlyDictionary<string, string>)item.Data);

            string cacheKey = BuildCacheKey(singleCommand);

            // Check cache first
            if (this.renderOptions.EnableRenderCache)
            {
                var cachedResult = await this.fusionCache
                    .TryGetAsync<byte[]>(cacheKey, token: cancellationToken)
                    .ConfigureAwait(false);

                if (cachedResult.HasValue && cachedResult.Value is { Length: > 0 } cachedBytes)
                {
                    string imageUri = await SaveCachedBytesAsync(singleCommand, jobId, cachedBytes, outputType, cancellationToken).ConfigureAwait(false);
                    return new BatchRenderResult
                    {
                        DisplayId = item.DisplayId,
                        Status = "rendered-cache",
                        ImageUri = new Uri(imageUri),
                    };
                }
            }

            // Render using pre-compiled template
            RenderJobResult renderResult = await SkiaRenderJobRenderer.RenderBatchItem(
                compiledTemplate,
                singleCommand,
                jobId,
                executionSettings,
                this.imageStorage,
                cancellationToken).ConfigureAwait(false);

            // Cache the result
            if (this.renderOptions.EnableRenderCache)
            {
                await CacheRenderedImageAsync(renderResult.ImageUri, cacheKey, cancellationToken).ConfigureAwait(false);
            }

            // Publish completion event
            Uri renderedImageUri = new(renderResult.ImageUri);
            RenderJobCompletedIntegrationEvent completedEvent = new(jobId, item.DisplayId, renderedImageUri);
            await this.messageBus
                .PublishAsync(completedEvent, new DeliveryOptions { TenantId = tenantId })
                .ConfigureAwait(false);

            return new BatchRenderResult
            {
                DisplayId = item.DisplayId,
                Status = "rendered",
                ImageUri = renderedImageUri,
            };
        }
        catch (Exception exception)
        {
            BatchFailureCounter.Add(1);
            this.logger.LogError(
                exception,
                "Batch item render failed. BatchId={BatchId} DisplayId={DisplayId}",
                batchId,
                item.DisplayId);

            return new BatchRenderResult
            {
                DisplayId = item.DisplayId,
                Status = "failed",
                ErrorCode = "ImageGenerator.RenderingFailed",
                ErrorMessage = exception.Message,
            };
        }
    }

    private async Task CacheRenderedImageAsync(string imageUri, string cacheKey, CancellationToken cancellationToken)
    {
        try
        {
            using Stream imageStream = await this.imageStorage.GetAsync(new Uri(imageUri), cancellationToken).ConfigureAwait(false);
            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            byte[] renderedBytes = memoryStream.ToArray();

            if (renderedBytes.Length <= Math.Max(1, this.renderOptions.MaxCachePayloadBytes))
            {
                TimeSpan cacheDuration = TimeSpan.FromSeconds(Math.Max(1, this.renderOptions.RenderCacheDurationSeconds));
                await this.fusionCache
                    .SetAsync(
                        cacheKey,
                        renderedBytes,
                        options => options
                            .SetDuration(cacheDuration)
                            .SetFailSafe(false),
                        token: cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            this.logger.LogWarning(exception, "Failed to cache rendered image for key {CacheKey}", cacheKey);
        }
    }

    private async Task<string> SaveCachedBytesAsync(
        SubmitRenderJobCommand request,
        Guid jobId,
        byte[] cachedBytes,
        string outputType,
        CancellationToken cancellationToken)
    {
        string extension = outputType switch
        {
            "jpeg" or "jpg" => "jpg",
            "webp" => "webp",
            "pam" => "pam",
            _ => "png",
        };

        string contentType = extension switch
        {
            "jpg" => "image/jpeg",
            "webp" => "image/webp",
            "pam" => "image/x-portable-arbitrarymap",
            _ => "image/png",
        };

        string path = $"{request.DisplayId:N}/{jobId:N}.{extension}";
        using var stream = new MemoryStream(cachedBytes);
        Uri uri = await this.imageStorage.SaveAsync(path, stream, contentType, cancellationToken).ConfigureAwait(false);
        return uri.ToString();
    }

    private static string BuildCacheKey(SubmitRenderJobCommand request)
    {
        // Reuse the same cache key logic as single renders for cache compatibility
        StringBuilder normalized = new(capacity: 2048);

        normalized.Append("v2|");
        normalized.Append(request.DisplayId.ToString("N", CultureInfo.InvariantCulture));
        normalized.Append('|');
        normalized.Append(request.OutputType.Trim().ToLowerInvariant());
        normalized.Append('|');

        foreach (string color in request.PaletteColors)
        {
            normalized.Append(color.Trim().ToUpperInvariant());
            normalized.Append(';');
        }

        normalized.Append('|');
        normalized.Append(request.Template.Width.ToString(CultureInfo.InvariantCulture));
        normalized.Append('x');
        normalized.Append(request.Template.Height.ToString(CultureInfo.InvariantCulture));
        normalized.Append('|');
        normalized.Append(request.Template.BackgroundColor.Trim().ToUpperInvariant());
        normalized.Append('|');
        normalized.Append(request.Template.UsePercentagePositioning ? '1' : '0');
        normalized.Append('|');

        foreach (SubmitRenderJobTemplateElementRequest element in request.Template.Elements)
        {
            normalized.Append(element.Type.Trim().ToLowerInvariant()).Append(':')
                .Append(element.Left.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(element.Top.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(element.Width.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(element.Height.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(element.Value.Trim()).Append(',')
                .Append(element.Binding.Trim()).Append(',')
                .Append(element.Format.Trim()).Append(',')
                .Append(element.FontFamily.Trim()).Append(',')
                .Append(element.FontSize.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(element.FontWeight.Trim()).Append(',')
                .Append(element.HorizontalAlign.Trim()).Append(',')
                .Append(element.ForegroundColor.Trim()).Append(',')
                .Append(element.BackgroundColor.Trim()).Append(',')
                .Append(element.StrokeWidth.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(element.CornerRadius.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(element.Fill ? '1' : '0').Append(',')
                .Append(element.WordWrap ? '1' : '0').Append(',')
                .Append(element.MaxLines.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(element.LineHeight.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(element.Ellipsis.Trim()).Append(',')
                .Append(element.AutoSize ? '1' : '0').Append(',')
                .Append(element.MinFontSize.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(element.MaxFontSize.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(element.TextEffect.Trim()).Append(',')
                .Append(element.ShowValue ? '1' : '0').Append(',')
                .Append(element.X1.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(element.Y1.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(element.X2.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(element.Y2.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(element.Padding.Trim()).Append(',')
                .Append(element.BadgeStyle.Trim()).Append(',')
                .Append(element.WasPrice.Trim()).Append(',')
                .Append(element.NowPrice.Trim()).Append(',')
                .Append(element.Currency.Trim())
                .Append(';');
        }

        normalized.Append('|');
        foreach ((string key, string value) in request.Data.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            normalized.Append(key.Trim()).Append('=').Append(value.Trim()).Append(';');
        }

        byte[] payloadBytes = Encoding.UTF8.GetBytes(normalized.ToString());
        byte[] hash = SHA256.HashData(payloadBytes);
        string hashHex = Convert.ToHexString(hash);

        return $"image-generator:render:{hashHex}";
    }
}
