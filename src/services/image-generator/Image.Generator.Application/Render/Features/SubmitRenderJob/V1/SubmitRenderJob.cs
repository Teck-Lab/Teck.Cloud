// <copyright file="SubmitRenderJob.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Security.Cryptography;
using ErrorOr;
using Image.Generator.Application.Storage;
using Microsoft.Extensions.Options;
using SharedKernel.Core.CQRS;
using SharedKernel.Events;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;

namespace Image.Generator.Application.Render.Features.SubmitRenderJob.V1;

/// <summary>
/// Represents a request to render an image for a device display.
/// </summary>
/// <param name="JobId">The caller-supplied job identifier. <see cref="Guid.Empty"/> indicates the handler should generate one.</param>
/// <param name="DisplayId">The device display identifier.</param>
/// <param name="TenantId">The tenant identifier that owns the render request.</param>
/// <param name="OutputType">The desired output format, for example <c>png</c>.</param>
/// <param name="PaletteColors">The palette colors to apply during render.</param>
/// <param name="Template">The rendering template payload.</param>
/// <param name="Data">The token data used for template bindings.</param>
/// <param name="ProviderProfile">Optional provider profile for vendor-specific output constraints.</param>
public sealed record SubmitRenderJobCommand(
    Guid JobId,
    Guid DisplayId,
    string TenantId,
    string OutputType,
    IReadOnlyList<string> PaletteColors,
    SubmitRenderJobTemplateRequest Template,
    IReadOnlyDictionary<string, string> Data,
    ProviderProfile? ProviderProfile = null)
    : ICommand<ErrorOr<SubmitRenderJobResponse>>;

/// <summary>
/// Handles render requests by enforcing concurrency limits and optionally using distributed cache.
/// </summary>
public sealed class SubmitRenderJobCommandHandler(
    RenderConcurrencyLimiter renderConcurrencyLimiter,
    IOptions<RenderProcessingOptions> renderOptions,
    ITenantFontAssetStore tenantFontAssetStore,
    IFusionCache fusionCache,
    IMessageBus messageBus,
    IImageStorage imageStorage,
    ILogger<SubmitRenderJobCommandHandler> logger)
    : ICommandHandler<SubmitRenderJobCommand, ErrorOr<SubmitRenderJobResponse>>
{
    private static readonly Meter RenderMeter = new("Teck.Cloud.ImageGenerator");
    private static readonly Counter<long> RenderRequestCounter = RenderMeter.CreateCounter<long>("image_generator.render.requests");
    private static readonly Counter<long> RenderFailureCounter = RenderMeter.CreateCounter<long>("image_generator.render.failures");
    private static readonly Counter<long> RenderCacheHitCounter = RenderMeter.CreateCounter<long>("image_generator.render.cache_hits");
    private static readonly Counter<long> RenderCacheMissCounter = RenderMeter.CreateCounter<long>("image_generator.render.cache_misses");
    private static readonly Counter<long> RenderBusyCounter = RenderMeter.CreateCounter<long>("image_generator.render.busy_rejections");
    private static readonly Histogram<double> RenderDurationMilliseconds = RenderMeter.CreateHistogram<double>("image_generator.render.duration_ms");
    private static readonly Histogram<double> RenderQueueWaitMilliseconds = RenderMeter.CreateHistogram<double>("image_generator.render.queue_wait_ms");

    private readonly RenderConcurrencyLimiter renderConcurrencyLimiter = renderConcurrencyLimiter;
    private readonly RenderProcessingOptions renderOptions = renderOptions.Value;
    private readonly ITenantFontAssetStore tenantFontAssetStore = tenantFontAssetStore;
    private readonly IFusionCache fusionCache = fusionCache;
    private readonly IMessageBus messageBus = messageBus;
    private readonly IImageStorage imageStorage = imageStorage;
    private readonly ILogger<SubmitRenderJobCommandHandler> logger = logger;

    /// <summary>
    /// Executes a render request and returns the render job metadata.
    /// </summary>
    /// <param name="request">The render request.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A successful response or a domain error describing the failure.</returns>
    public async ValueTask<ErrorOr<SubmitRenderJobResponse>> Handle(
        SubmitRenderJobCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Stopwatch totalStopwatch = Stopwatch.StartNew();
        RenderRequestCounter.Add(1);

        Guid jobId = request.JobId != Guid.Empty ? request.JobId : Guid.NewGuid();
        string outputType = request.OutputType.Trim().ToLowerInvariant();
        string cacheKey = BuildCacheKey(request);

        try
        {
            using RenderConcurrencyLimiter.RenderConcurrencyLease lease =
                await this.renderConcurrencyLimiter.TryEnterAsync(cancellationToken).ConfigureAwait(false);

            RenderQueueWaitMilliseconds.Record(lease.WaitDuration.TotalMilliseconds);

            if (!lease.IsAcquired)
            {
                RenderBusyCounter.Add(1);
                this.logger.LogWarning(
                    "Render request rejected due to queue timeout after {QueueWaitMs} ms. DisplayId={DisplayId} MaxConcurrentRenders={MaxConcurrentRenders}",
                    lease.WaitDuration.TotalMilliseconds,
                    request.DisplayId,
                    this.renderConcurrencyLimiter.MaxConcurrentRenders);

                return Error.Unexpected(
                    code: "ImageGenerator.Busy",
                    description: "Render queue is saturated. Retry the request.");
            }

            string imageUri;
            bool cacheHit = false;

            if (this.renderOptions.EnableRenderCache)
            {
                var cachedResult = await this.fusionCache
                    .TryGetAsync<byte[]>(cacheKey, token: cancellationToken)
                    .ConfigureAwait(false);

                if (cachedResult.HasValue && cachedResult.Value is { Length: > 0 } cachedBytes)
                {
                    cacheHit = true;
                    RenderCacheHitCounter.Add(1);
                    imageUri = await SaveCachedBytesAsync(request, jobId, cachedBytes, outputType, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    RenderCacheMissCounter.Add(1);
                    imageUri = await RenderAndSaveAsync(request, jobId, cacheKey, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                imageUri = await RenderAndSaveAsync(request, jobId, cacheKey, cancellationToken).ConfigureAwait(false);
            }

            SubmitRenderJobResponse response = new()
            {
                JobId = jobId,
                Status = cacheHit ? "rendered-cache" : "rendered",
                ImageUri = string.IsNullOrWhiteSpace(imageUri) ? null : new Uri(imageUri),
                QueuedAtUtc = DateTimeOffset.UtcNow,
            };

            RenderDurationMilliseconds.Record(totalStopwatch.Elapsed.TotalMilliseconds);

            if (!string.IsNullOrWhiteSpace(imageUri))
            {
                Uri renderedImageUri = new(imageUri);
                RenderJobCompletedIntegrationEvent completedEvent = new(jobId, request.DisplayId, renderedImageUri);
                await this.messageBus
                    .PublishAsync(completedEvent, new DeliveryOptions { TenantId = request.TenantId })
                    .ConfigureAwait(false);
            }

            return response;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            RenderFailureCounter.Add(1);
            RenderDurationMilliseconds.Record(totalStopwatch.Elapsed.TotalMilliseconds);

            this.logger.LogError(
                exception,
                "Rendering failed. DisplayId={DisplayId} OutputType={OutputType}",
                request.DisplayId,
                outputType);

            return Error.Unexpected(
                code: "ImageGenerator.RenderingFailed",
                description: $"Rendering failed for display '{request.DisplayId}': {exception.Message}");
        }
    }

    private async Task<string> RenderAndSaveAsync(
        SubmitRenderJobCommand request,
        Guid jobId,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string> tenantFontPaths = await this.tenantFontAssetStore
            .EnsureFontsAvailableAsync(
                request.TenantId,
                request.Template.Elements.Select(static element => element.FontFamily).ToArray(),
                cancellationToken)
            .ConfigureAwait(false);

        RenderJobResult renderResult = await SkiaRenderJobRenderer.Render(
            request,
            jobId,
            new RenderExecutionSettings(
                this.renderOptions.StrictBindings,
                this.renderOptions.PaletteCancellationCheckStridePixels,
                tenantFontPaths),
            this.imageStorage,
            cancellationToken).ConfigureAwait(false);

        if (this.renderOptions.EnableRenderCache)
        {
            using Stream imageStream = await this.imageStorage.GetAsync(new Uri(renderResult.ImageUri), cancellationToken).ConfigureAwait(false);
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

        return renderResult.ImageUri;
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

        string path = $"{request.DisplayId:N}/{jobId:N}.{extension}";
        string contentType = extension switch
        {
            "jpg" => "image/jpeg",
            "webp" => "image/webp",
            "pam" => "image/x-portable-arbitrarymap",
            _ => "image/png",
        };

        using var stream = new MemoryStream(cachedBytes);
        Uri uri = await this.imageStorage.SaveAsync(path, stream, contentType, cancellationToken).ConfigureAwait(false);
        return uri.ToString();
    }

    private static string BuildCacheKey(SubmitRenderJobCommand request)
    {
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
                .Append(element.Currency.Trim()).Append(',')
                .Append(element.TextDirection.Trim()).Append(',')
                .Append(element.GradientType.Trim()).Append(',')
                .Append(element.GradientAngle.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(element.GradientStartX.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(element.GradientStartY.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(element.GradientEndX.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(element.GradientEndY.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(element.ElementId.Trim())
                .Append(';');
        }

        normalized.Append('|');
        foreach ((string key, string value) in request.Data.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            normalized.Append(key.Trim()).Append('=').Append(value.Trim()).Append(';');
        }

        if (request.ProviderProfile is not null)
        {
            normalized.Append("|provider=")
                .Append(request.ProviderProfile.ProviderName).Append(',')
                .Append(request.ProviderProfile.ScreenWidth).Append('x')
                .Append(request.ProviderProfile.ScreenHeight).Append(',')
                .Append(request.ProviderProfile.ColorDepth).Append(',')
                .Append(request.ProviderProfile.PreferredFormat).Append(',')
                .Append(request.ProviderProfile.DitherLevel).Append(',')
                .Append(request.ProviderProfile.QuantizeToPalette ? '1' : '0').Append(',');

            foreach (string color in request.ProviderProfile.SupportedColors.Order(StringComparer.OrdinalIgnoreCase))
            {
                normalized.Append(color.Trim().ToUpperInvariant()).Append('.');
            }

            normalized.Append(';');
        }

        byte[] payloadBytes = Encoding.UTF8.GetBytes(normalized.ToString());
        byte[] hash = SHA256.HashData(payloadBytes);
        string hashHex = Convert.ToHexString(hash);

        return $"image-generator:render:{hashHex}";
    }
}
