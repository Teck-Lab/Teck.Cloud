// <copyright file="SkiaRenderJobRenderer.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using Image.Generator.Application.Storage;
using ImageMagick;
using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.SkiaSharp;
using ZXing.SkiaSharp.Rendering;

namespace Image.Generator.Application.Render.Features.SubmitRenderJob.V1;

internal sealed record RenderExecutionSettings(
    bool StrictBindings,
    int PaletteCancellationCheckStridePixels,
    IReadOnlyDictionary<string, string>? TenantFontPaths);

internal sealed record RenderJobResult(string ImageUri, int Width, int Height, int ElementCount);

internal static class SkiaRenderJobRenderer
{
    private const float DefaultFontSize = 16;
    private const int DefaultBarcodeSize = 180;
    private const string DefaultFontFamily = "Arial";

    private sealed record RgbColor(byte Red, byte Green, byte Blue);

    private static readonly IReadOnlyDictionary<string, string> EmptyTenantFontPaths = new Dictionary<string, string>(0, StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, SKTypeface> TypefaceCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, RgbColor[]> PaletteCache = new(StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, IProviderEncoder> ProviderEncoders = new Dictionary<string, IProviderEncoder>(StringComparer.OrdinalIgnoreCase)
    {
        ["hanshow"] = new ProviderEncoders.HanshowBinaryEncoder(),
        ["pricer"] = new ProviderEncoders.PricerPpmEncoder(),
        ["ses"] = new ProviderEncoders.SesPngEncoder(),
        ["solum"] = new ProviderEncoders.SolumBmpEncoder(),
    };

    public static async ValueTask<RenderJobResult> Render(
        SubmitRenderJobCommand request,
        Guid jobId,
        RenderExecutionSettings settings,
        IImageStorage imageStorage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(imageStorage);

        ProviderProfile? provider = request.ProviderProfile;
        (MagickFormat magickFormat, string extension, int quality) = ResolveOutputType(provider?.PreferredFormat ?? request.OutputType);

        int width = provider?.ScreenWidth > 0 ? provider.ScreenWidth : Math.Max(1, request.Template.Width);
        int height = provider?.ScreenHeight > 0 ? provider.ScreenHeight : Math.Max(1, request.Template.Height);

        using SKSurface surface = SKSurface.Create(new SKImageInfo(width, height));
        SKCanvas canvas = surface.Canvas;
        IReadOnlyDictionary<string, string> tenantFontPaths = settings.TenantFontPaths ?? EmptyTenantFontPaths;

        canvas.Clear(ParseColorOrDefault(request.Template.BackgroundColor, SKColors.White));

        foreach (SubmitRenderJobTemplateElementRequest element in request.Template.Elements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DrawTemplateElement(canvas, element, request.Data, settings.StrictBindings, tenantFontPaths, width, height);
        }

        using SKImage image = surface.Snapshot();
        SKImage outputImage = image;

        // Apply provider palette quantization if requested
        if (provider?.QuantizeToPalette == true && provider.SupportedColors.Count > 0)
        {
            outputImage = ApplyProviderPalette(image, (IReadOnlyList<string>)provider.SupportedColors);
        }

        using var memoryStream = new MemoryStream();
        string contentType;

        if (provider is not null && ProviderEncoders.TryGetValue(provider.ProviderName, out IProviderEncoder? encoder))
        {
            byte[] encoded = encoder.Encode(outputImage, provider);
            await memoryStream.WriteAsync(encoded, cancellationToken).ConfigureAwait(false);
            contentType = provider.PreferredFormat.ToLowerInvariant() switch
            {
                "bmp" => "image/bmp",
                "ppm" => "image/x-portable-pixmap",
                "png" => "image/png",
                _ => "application/octet-stream",
            };
        }
        else
        {
            WriteWithMagick(
                outputImage,
                memoryStream,
                magickFormat,
                quality,
                request.PaletteColors,
                settings.PaletteCancellationCheckStridePixels,
                cancellationToken);

            contentType = magickFormat switch
            {
                MagickFormat.Jpeg => "image/jpeg",
                MagickFormat.WebP => "image/webp",
                MagickFormat.Pam => "image/x-portable-arbitrarymap",
                _ => "image/png",
            };
        }

        memoryStream.Position = 0;

        string path = $"{request.DisplayId:N}/{jobId:N}.{extension}";

        Uri imageUri = await imageStorage.SaveAsync(path, memoryStream, contentType, cancellationToken).ConfigureAwait(false);

        return new RenderJobResult(imageUri.ToString(), width, height, request.Template.Elements.Count);
    }

    /// <summary>
    /// Renders a single batch item using a pre-compiled template and optional surface pooling.
    /// </summary>
    public static async ValueTask<RenderJobResult> RenderBatchItem(
        CompiledTemplate compiledTemplate,
        SubmitRenderJobCommand request,
        Guid jobId,
        RenderExecutionSettings settings,
        IImageStorage imageStorage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(compiledTemplate);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(imageStorage);

        ProviderProfile? provider = request.ProviderProfile;
        (MagickFormat magickFormat, string extension, int quality) = ResolveOutputType(provider?.PreferredFormat ?? request.OutputType);

        int width = provider?.ScreenWidth > 0 ? provider.ScreenWidth : compiledTemplate.Width;
        int height = provider?.ScreenHeight > 0 ? provider.ScreenHeight : compiledTemplate.Height;

        using SKSurface surface = SKSurface.Create(new SKImageInfo(width, height));
        SKCanvas canvas = surface.Canvas;
        IReadOnlyDictionary<string, string> tenantFontPaths = settings.TenantFontPaths ?? EmptyTenantFontPaths;

        canvas.Clear(compiledTemplate.BackgroundColor);

        foreach (CompiledElement element in compiledTemplate.Elements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DrawCompiledElement(canvas, element, request.Data, settings.StrictBindings, tenantFontPaths, width, height);
        }

        using SKImage image = surface.Snapshot();
        SKImage outputImage = image;

        if (provider?.QuantizeToPalette == true && provider.SupportedColors.Count > 0)
        {
            outputImage = ApplyProviderPalette(image, (IReadOnlyList<string>)provider.SupportedColors);
        }

        using var memoryStream = new MemoryStream();
        string contentType;

        if (provider is not null && ProviderEncoders.TryGetValue(provider.ProviderName, out IProviderEncoder? encoder))
        {
            byte[] encoded = encoder.Encode(outputImage, provider);
            await memoryStream.WriteAsync(encoded, cancellationToken).ConfigureAwait(false);
            contentType = provider.PreferredFormat.ToLowerInvariant() switch
            {
                "bmp" => "image/bmp",
                "ppm" => "image/x-portable-pixmap",
                "png" => "image/png",
                _ => "application/octet-stream",
            };
        }
        else
        {
            WriteWithMagick(
                outputImage,
                memoryStream,
                magickFormat,
                quality,
                request.PaletteColors,
                settings.PaletteCancellationCheckStridePixels,
                cancellationToken);

            contentType = magickFormat switch
            {
                MagickFormat.Jpeg => "image/jpeg",
                MagickFormat.WebP => "image/webp",
                MagickFormat.Pam => "image/x-portable-arbitrarymap",
                _ => "image/png",
            };
        }

        memoryStream.Position = 0;

        string path = $"{request.DisplayId:N}/{jobId:N}.{extension}";

        Uri imageUri = await imageStorage.SaveAsync(path, memoryStream, contentType, cancellationToken).ConfigureAwait(false);

        return new RenderJobResult(imageUri.ToString(), width, height, compiledTemplate.Elements.Count);
    }

    public static string BuildOutputPath(Guid displayId, string outputTypeOrExtension, Guid jobId)
    {
        string extension = NormalizeOutputExtension(outputTypeOrExtension);
        string outputDirectory = Path.Combine(
            Path.GetTempPath(),
            "teck-cloud",
            "image-generator",
            "jobs",
            displayId.ToString("N", CultureInfo.InvariantCulture));

        Directory.CreateDirectory(outputDirectory);

        return Path.Combine(outputDirectory, $"{jobId:N}.{extension}");
    }

    private static void DrawTemplateElement(
        SKCanvas canvas,
        SubmitRenderJobTemplateElementRequest element,
        IReadOnlyDictionary<string, string> data,
        bool strictBindings,
        IReadOnlyDictionary<string, string> tenantFontPaths,
        int templateWidth,
        int templateHeight)
    {
        string normalizedType = element.Type.Trim().ToLowerInvariant();

        // Resolve percentage-based positioning if enabled
        element = ResolvePercentages(element, templateWidth, templateHeight);

        if (normalizedType == "text")
        {
            DrawTextElement(canvas, element, data, strictBindings, tenantFontPaths);
            return;
        }

        if (normalizedType is "barcode" or "code128" or "qrcode" or "datamatrix" or "pdf417" or "ean13" or "upca" or "code39" or "itf" or "aztec")
        {
            DrawCodeElement(canvas, element, data, strictBindings);
            return;
        }

        if (normalizedType == "rectangle")
        {
            DrawRectangleElement(canvas, element);
            return;
        }

        if (normalizedType == "circle")
        {
            DrawCircleElement(canvas, element);
            return;
        }

        if (normalizedType == "image")
        {
            DrawImageElement(canvas, element, data, strictBindings);
            return;
        }

        if (normalizedType == "line")
        {
            DrawLineElement(canvas, element);
            return;
        }

        if (normalizedType == "container")
        {
            DrawContainerElement(canvas, element, data, strictBindings, tenantFontPaths, templateWidth, templateHeight);
            return;
        }

        if (normalizedType == "badge")
        {
            DrawBadgeElement(canvas, element);
            return;
        }

        if (normalizedType == "price")
        {
            DrawPriceElement(canvas, element, data, strictBindings);
            return;
        }

        throw new InvalidOperationException($"Unsupported template element type '{element.Type}'.");
    }

    private static void DrawCompiledElement(
        SKCanvas canvas,
        CompiledElement element,
        IReadOnlyDictionary<string, string> data,
        bool strictBindings,
        IReadOnlyDictionary<string, string> tenantFontPaths,
        int templateWidth,
        int templateHeight)
    {
        string normalizedType = element.NormalizedType;

        // Resolve percentage-based positioning if enabled
        (float left, float top, float width, float height) = ResolveCompiledPercentages(
            element, templateWidth, templateHeight);

        // Create a positioned version for drawing
        CompiledElement positionedElement = element with
        {
            Left = left,
            Top = top,
            Width = width,
            Height = height,
        };

        if (normalizedType == "text")
        {
            DrawCompiledTextElement(canvas, positionedElement, data, strictBindings, tenantFontPaths);
            return;
        }

        if (normalizedType is "barcode" or "code128" or "qrcode" or "datamatrix" or "pdf417" or "ean13" or "upca" or "code39" or "itf" or "aztec")
        {
            DrawCompiledCodeElement(canvas, positionedElement, data, strictBindings);
            return;
        }

        if (normalizedType == "rectangle")
        {
            DrawCompiledRectangleElement(canvas, positionedElement);
            return;
        }

        if (normalizedType == "circle")
        {
            DrawCompiledCircleElement(canvas, positionedElement);
            return;
        }

        if (normalizedType == "image")
        {
            DrawCompiledImageElement(canvas, positionedElement, data, strictBindings);
            return;
        }

        if (normalizedType == "line")
        {
            DrawCompiledLineElement(canvas, positionedElement);
            return;
        }

        if (normalizedType == "container")
        {
            DrawCompiledContainerElement(canvas, positionedElement, data, strictBindings, tenantFontPaths, templateWidth, templateHeight);
            return;
        }

        if (normalizedType == "badge")
        {
            DrawCompiledBadgeElement(canvas, positionedElement);
            return;
        }

        if (normalizedType == "price")
        {
            DrawCompiledPriceElement(canvas, positionedElement, data, strictBindings);
            return;
        }

        throw new InvalidOperationException($"Unsupported compiled element type '{element.NormalizedType}'.");
    }

    private static void DrawTextElement(
        SKCanvas canvas,
        SubmitRenderJobTemplateElementRequest element,
        IReadOnlyDictionary<string, string> data,
        bool strictBindings,
        IReadOnlyDictionary<string, string> tenantFontPaths)
    {
        string value = ResolveValue(element, data, strictBindings);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        SKTypeface typeface = ResolveTypeface(element.FontFamily, element.FontWeight, tenantFontPaths);
        float fontSize = element.FontSize > 0 ? element.FontSize : DefaultFontSize;

        // Auto-size: binary search font size to fit bounds
        if (element.AutoSize && element.Width > 0 && element.Height > 0)
        {
            fontSize = ResolveAutoFontSize(value, typeface, element);
        }

        float lineHeightPx = fontSize * Math.Max(0.1f, element.LineHeight);
        (float anchorX, SKTextAlign align) = ResolveHorizontalAlignment(element);

        using SKFont font = new(typeface, fontSize);
        SKColor foregroundColor = ParseColorOrDefault(element.ForegroundColor, SKColors.Black);

        // Word wrap into lines
        IReadOnlyList<string> lines = element.WordWrap && element.Width > 0
            ? WrapText(value, font, element.Width, element.MaxLines, element.Ellipsis)
            : [value];

        // Compute starting Y to vertically center multi-line text within element bounds
        float totalTextHeight = lines.Count * lineHeightPx;
        float startY = element.Height > 0
            ? element.Top + ((element.Height - totalTextHeight) / 2f) + (fontSize * 0.8f)
            : element.Top + fontSize;

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                startY += lineHeightPx;
                continue;
            }

            // Shadow effect
            if (element.TextEffect.Equals("shadow", StringComparison.OrdinalIgnoreCase))
            {
                using SKPaint shadowPaint = new()
                {
                    IsAntialias = true,
                    Color = new SKColor(0, 0, 0, 80),
                    Style = SKPaintStyle.Fill,
                };
                canvas.DrawText(line, anchorX + 1, startY + 1, align, font, shadowPaint);
            }

            // Outline effect
            if (element.TextEffect.Equals("outline", StringComparison.OrdinalIgnoreCase))
            {
                using SKPaint outlinePaint = new()
                {
                    IsAntialias = true,
                    Color = SKColors.White,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = Math.Max(1, fontSize / 8f),
                };
                canvas.DrawText(line, anchorX, startY, align, font, outlinePaint);
            }

            // Main text
            using (SKPaint paint = new() { IsAntialias = true, Color = foregroundColor })
            {
                canvas.DrawText(line, anchorX, startY, align, font, paint);
            }

            // Strikethrough effect
            if (element.TextEffect.Equals("strikethrough", StringComparison.OrdinalIgnoreCase))
            {
                float lineWidth = font.MeasureText(line);
                float lineX = align switch
                {
                    SKTextAlign.Center => anchorX - (lineWidth / 2f),
                    SKTextAlign.Right => anchorX - lineWidth,
                    _ => anchorX,
                };
                float strikeY = startY - (fontSize * 0.3f);

                using SKPaint strikePaint = new()
                {
                    IsAntialias = true,
                    Color = foregroundColor,
                    StrokeWidth = Math.Max(1, fontSize / 10f),
                };
                canvas.DrawLine(lineX, strikeY, lineX + lineWidth, strikeY, strikePaint);
            }

            startY += lineHeightPx;
        }
    }

    private static void DrawCodeElement(
        SKCanvas canvas,
        SubmitRenderJobTemplateElementRequest element,
        IReadOnlyDictionary<string, string> data,
        bool strictBindings)
    {
        string value = ResolveValue(element, data, strictBindings);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        int width = (int)MathF.Round(element.Width <= 0 ? DefaultBarcodeSize : element.Width);
        int height = (int)MathF.Round(element.Height <= 0 ? DefaultBarcodeSize : element.Height);
        BarcodeFormat format = ResolveBarcodeFormat(element.Type, element.Format);

        using SKBitmap barcodeBitmap = CreateBarcode(format, value, Math.Max(1, width), Math.Max(1, height));
        canvas.DrawBitmap(
            barcodeBitmap,
            new SKRect(element.Left, element.Top, element.Left + width, element.Top + height));

        // Human-readable text below barcode
        if (element.ShowValue && !string.IsNullOrWhiteSpace(value))
        {
            float textY = element.Top + height + 4;
            float textFontSize = Math.Max(8, Math.Min(14, width / (value.Length * 0.6f)));

            using SKFont textFont = new(SKTypeface.Default, textFontSize);
            using SKPaint textPaint = new()
            {
                IsAntialias = true,
                Color = ParseColorOrDefault(element.ForegroundColor, SKColors.Black),
            };

            canvas.DrawText(
                value,
                element.Left + (width / 2f),
                textY,
                SKTextAlign.Center,
                textFont,
                textPaint);
        }
    }

    private static void DrawRectangleElement(SKCanvas canvas, SubmitRenderJobTemplateElementRequest element)
    {
        float width = Math.Max(1, element.Width);
        float height = Math.Max(1, element.Height);
        SKRect rect = new(element.Left, element.Top, element.Left + width, element.Top + height);
        float cornerRadius = Math.Max(0, element.CornerRadius);

        bool hasBackground = !string.IsNullOrWhiteSpace(element.BackgroundColor);
        bool shouldFill = element.Fill || hasBackground;
        if (shouldFill)
        {
            using SKPaint fillPaint = new()
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = ParseColorOrDefault(element.BackgroundColor, SKColors.White),
            };

            DrawRoundedRect(canvas, rect, cornerRadius, fillPaint);
        }

        float strokeWidth = 1;
        if (element.StrokeWidth > 0)
        {
            strokeWidth = element.StrokeWidth;
        }
        else if (shouldFill)
        {
            strokeWidth = 0;
        }

        if (strokeWidth > 0)
        {
            using SKPaint strokePaint = new()
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = strokeWidth,
                Color = ParseColorOrDefault(element.ForegroundColor, SKColors.Black),
            };

            DrawRoundedRect(canvas, rect, cornerRadius, strokePaint);
        }
    }

    private static void DrawCircleElement(SKCanvas canvas, SubmitRenderJobTemplateElementRequest element)
    {
        float width = Math.Max(1, element.Width);
        float height = Math.Max(1, element.Height);
        float centerX = element.Left + (width / 2f);
        float centerY = element.Top + (height / 2f);
        float radiusX = width / 2f;
        float radiusY = height / 2f;

        bool hasBackground = !string.IsNullOrWhiteSpace(element.BackgroundColor);
        bool shouldFill = element.Fill || hasBackground;

        if (shouldFill)
        {
            using SKPaint fillPaint = new()
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = ParseColorOrDefault(element.BackgroundColor, SKColors.White),
            };

            if (Math.Abs(radiusX - radiusY) < 0.5f)
            {
                canvas.DrawCircle(centerX, centerY, radiusX, fillPaint);
            }
            else
            {
                SKRect ovalRect = new(element.Left, element.Top, element.Left + width, element.Top + height);
                canvas.DrawOval(ovalRect, fillPaint);
            }
        }

        float strokeWidth = 1;
        if (element.StrokeWidth > 0)
        {
            strokeWidth = element.StrokeWidth;
        }
        else if (shouldFill)
        {
            strokeWidth = 0;
        }

        if (strokeWidth > 0)
        {
            using SKPaint strokePaint = new()
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = strokeWidth,
                Color = ParseColorOrDefault(element.ForegroundColor, SKColors.Black),
            };

            if (Math.Abs(radiusX - radiusY) < 0.5f)
            {
                canvas.DrawCircle(centerX, centerY, radiusX, strokePaint);
            }
            else
            {
                SKRect ovalRect = new(element.Left, element.Top, element.Left + width, element.Top + height);
                canvas.DrawOval(ovalRect, strokePaint);
            }
        }
    }

    private static void DrawImageElement(
        SKCanvas canvas,
        SubmitRenderJobTemplateElementRequest element,
        IReadOnlyDictionary<string, string> data,
        bool strictBindings)
    {
        string value = ResolveValue(element, data, strictBindings);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string imagePath = value.Trim();
        if (!File.Exists(imagePath))
        {
            return;
        }

        using SKBitmap? bitmap = SKBitmap.Decode(imagePath);
        if (bitmap is null)
        {
            return;
        }

        float destWidth = Math.Max(1, element.Width);
        float destHeight = Math.Max(1, element.Height);
        SKRect destRect = new(element.Left, element.Top, element.Left + destWidth, element.Top + destHeight);
        canvas.DrawBitmap(bitmap, destRect);
    }

    private static void DrawLineElement(SKCanvas canvas, SubmitRenderJobTemplateElementRequest element)
    {
        using SKPaint paint = new()
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(1, element.StrokeWidth),
            Color = ParseColorOrDefault(element.ForegroundColor, SKColors.Black),
        };

        canvas.DrawLine(element.X1, element.Y1, element.X2, element.Y2, paint);
    }

    private static float ResolveAutoFontSize(
        string text,
        SKTypeface typeface,
        SubmitRenderJobTemplateElementRequest element)
    {
        float minSize = element.MinFontSize > 0 ? element.MinFontSize : 8;
        float maxSize = element.MaxFontSize > 0 ? element.MaxFontSize : 72;
        float targetWidth = element.Width;
        float targetHeight = element.Height;

        if (string.IsNullOrWhiteSpace(text) || targetWidth <= 0 || targetHeight <= 0)
        {
            return element.FontSize > 0 ? element.FontSize : DefaultFontSize;
        }

        float bestSize = minSize;
        float low = minSize;
        float high = maxSize;

        while (low <= high)
        {
            float mid = (low + high) / 2f;
            using SKFont font = new(typeface, mid);

            float measuredWidth = 0;
            if (element.WordWrap)
            {
                IReadOnlyList<string> lines = WrapText(text, font, targetWidth, 0, string.Empty);
                float lineHeight = mid * Math.Max(0.1f, element.LineHeight);
                float totalHeight = lines.Count * lineHeight;
                if (totalHeight <= targetHeight)
                {
                    bestSize = mid;
                    low = mid + 0.5f;
                    continue;
                }
            }
            else
            {
                measuredWidth = font.MeasureText(text);
                if (measuredWidth <= targetWidth && mid <= targetHeight)
                {
                    bestSize = mid;
                    low = mid + 0.5f;
                    continue;
                }
            }

            high = mid - 0.5f;
        }

        return bestSize;
    }

    private static IReadOnlyList<string> WrapText(
        string text,
        SKFont font,
        float maxWidth,
        int maxLines,
        string ellipsis)
    {
        if (string.IsNullOrWhiteSpace(text) || maxWidth <= 0)
        {
            return [text];
        }

        List<string> lines = [];
        ReadOnlySpan<char> remaining = text.AsSpan();

        while (!remaining.IsEmpty)
        {
            int breakIndex = FindWrapBreakpoint(remaining, font, maxWidth);
            if (breakIndex <= 0)
            {
                // Emergency break: force at least one character
                breakIndex = 1;
            }

            lines.Add(remaining[..breakIndex].ToString());
            remaining = remaining[breakIndex..].TrimStart();

            if (maxLines > 0 && lines.Count >= maxLines)
            {
                if (!remaining.IsEmpty && !string.IsNullOrEmpty(ellipsis))
                {
                    string lastLine = lines[^1];
                    string truncated = lastLine[..Math.Max(0, lastLine.Length - ellipsis.Length)] + ellipsis;
                    lines[^1] = truncated;
                }

                break;
            }
        }

        return lines.Count > 0 ? lines : [text];
    }

    private static int FindWrapBreakpoint(ReadOnlySpan<char> text, SKFont font, float maxWidth)
    {
        if (text.IsEmpty)
        {
            return 0;
        }

        // Prefer breaking at whitespace
        int lastWhitespace = -1;
        float currentWidth = 0;

        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            float charWidth = font.MeasureText(text.Slice(index, 1));

            if (currentWidth + charWidth > maxWidth)
            {
                return lastWhitespace > 0 ? lastWhitespace : index;
            }

            currentWidth += charWidth;

            if (char.IsWhiteSpace(character))
            {
                lastWhitespace = index + 1;
            }
        }

        return text.Length;
    }

    private static bool IsRightToLeftText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Any(character => character is >= '\u0590' and <= '\u08FF' or >= '\uFB1D' and <= '\uFDFF' or >= '\uFE70' and <= '\uFEFF');
    }

    private static string ResolveTextDirection(string text, string textDirection)
    {
        string normalized = textDirection.Trim().ToLowerInvariant();
        if (normalized == "auto")
        {
            return IsRightToLeftText(text) ? "rtl" : "ltr";
        }

        return normalized == "rtl" ? "rtl" : "ltr";
    }

    private static SKShader? CreateGradientShader(CompiledElement element, SKRect bounds)
    {
        if (element.GradientType == "none" || element.GradientColors.Count < 2)
        {
            return null;
        }

        SKColor[] colors = element.GradientColors.ToArray();
        float[] positions = new float[colors.Length];
        for (int colorIndex = 0; colorIndex < colors.Length; colorIndex++)
        {
            positions[colorIndex] = colorIndex / (float)(colors.Length - 1);
        }

        if (element.GradientType == "radial")
        {
            float centerX = bounds.MidX;
            float centerY = bounds.MidY;
            float radius = Math.Max(bounds.Width, bounds.Height) / 2f;
            return SKShader.CreateRadialGradient(new SKPoint(centerX, centerY), radius, colors, positions, SKShaderTileMode.Clamp);
        }

        // Linear gradient
        float startX, startY, endX, endY;
        const float epsilon = 0.0001f;
        bool hasExplicitGradientEndpoints =
            Math.Abs(element.GradientStartX) > epsilon
            || Math.Abs(element.GradientStartY) > epsilon
            || Math.Abs(element.GradientEndX) > epsilon
            || Math.Abs(element.GradientEndY) > epsilon;

        if (hasExplicitGradientEndpoints)
        {
            startX = bounds.Left + (bounds.Width * element.GradientStartX);
            startY = bounds.Top + (bounds.Height * element.GradientStartY);
            endX = bounds.Left + (bounds.Width * element.GradientEndX);
            endY = bounds.Top + (bounds.Height * element.GradientEndY);
        }
        else
        {
            float angleRad = element.GradientAngle * MathF.PI / 180f;
            float dx = MathF.Cos(angleRad) * bounds.Width / 2f;
            float dy = MathF.Sin(angleRad) * bounds.Height / 2f;
            startX = bounds.MidX - dx;
            startY = bounds.MidY - dy;
            endX = bounds.MidX + dx;
            endY = bounds.MidY + dy;
        }

        return SKShader.CreateLinearGradient(new SKPoint(startX, startY), new SKPoint(endX, endY), colors, positions, SKShaderTileMode.Clamp);
    }

    private static SKTextAlign SwapAlignment(SKTextAlign align)
    {
        return align switch
        {
            SKTextAlign.Left => SKTextAlign.Right,
            SKTextAlign.Right => SKTextAlign.Left,
            _ => align,
        };
    }

    private static float SwapAnchorX(float anchorX, SKTextAlign align, CompiledElement element)
    {
        if (element.Width <= 0)
        {
            return anchorX;
        }

        return align switch
        {
            SKTextAlign.Left => element.Left + element.Width,
            SKTextAlign.Right => element.Left,
            _ => anchorX,
        };
    }

    private static void DrawRoundedRect(SKCanvas canvas, SKRect rect, float cornerRadius, SKPaint paint)
    {
        if (cornerRadius <= 0)
        {
            canvas.DrawRect(rect, paint);
            return;
        }

        canvas.DrawRoundRect(rect, cornerRadius, cornerRadius, paint);
    }

    private static string ResolveValue(
        SubmitRenderJobTemplateElementRequest element,
        IReadOnlyDictionary<string, string> data,
        bool strictBindings)
    {
        if (!string.IsNullOrWhiteSpace(element.Binding))
        {
            string bindingKey = element.Binding.Trim();
            if (data.TryGetValue(bindingKey, out string? boundValue))
            {
                return boundValue ?? string.Empty;
            }

            if (strictBindings)
            {
                throw new InvalidOperationException($"Missing binding key '{bindingKey}' in template data.");
            }
        }

        if (string.IsNullOrWhiteSpace(element.Value))
        {
            return string.Empty;
        }

        string resolved = element.Value;
        foreach ((string key, string itemValue) in data)
        {
            resolved = resolved.Replace($"{{{{{key}}}}}", itemValue ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        if (strictBindings
            && resolved.Contains("{{", StringComparison.Ordinal)
            && resolved.Contains("}}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unresolved template token in value '{element.Value}'.");
        }

        return resolved;
    }

    private static (float AnchorX, SKTextAlign Align) ResolveHorizontalAlignment(SubmitRenderJobTemplateElementRequest element)
    {
        if (element.HorizontalAlign.Equals("center", StringComparison.OrdinalIgnoreCase) && element.Width > 0)
        {
            return (element.Left + (element.Width / 2f), SKTextAlign.Center);
        }

        if (element.HorizontalAlign.Equals("right", StringComparison.OrdinalIgnoreCase) && element.Width > 0)
        {
            return (element.Left + element.Width, SKTextAlign.Right);
        }

        return (element.Left, SKTextAlign.Left);
    }

    private static SKTypeface ResolveTypeface(
        string fontFamily,
        string fontWeight,
        IReadOnlyDictionary<string, string> tenantFontPaths)
    {
        if (TenantFontKeys.TryParseTenantFontKey(fontFamily, out string tenantFontKey)
            && tenantFontPaths.TryGetValue(tenantFontKey, out string? fontPath)
            && !string.IsNullOrWhiteSpace(fontPath)
            && File.Exists(fontPath))
        {
            return TypefaceCache.GetOrAdd(
                $"tenant-font:{fontPath}",
                _ => SKTypeface.FromFile(fontPath) ?? SKTypeface.Default);
        }

        SKFontStyle fontStyle = fontWeight.Contains("bold", StringComparison.OrdinalIgnoreCase)
            ? SKFontStyle.Bold
            : SKFontStyle.Normal;

        string normalizedFamily = string.IsNullOrWhiteSpace(fontFamily)
            ? DefaultFontFamily
            : fontFamily.Trim();

        string cacheKey = $"{normalizedFamily}|{fontStyle.Weight}|{fontStyle.Width}|{fontStyle.Slant}";

        return TypefaceCache.GetOrAdd(
            cacheKey,
            _ => SKTypeface.FromFamilyName(normalizedFamily, fontStyle)
                ?? SKTypeface.FromFamilyName(DefaultFontFamily, fontStyle)
                ?? SKTypeface.Default);
    }

    private static SubmitRenderJobTemplateElementRequest ResolvePercentages(
        SubmitRenderJobTemplateElementRequest element,
        int templateWidth,
        int templateHeight)
    {
        if (templateWidth <= 0 || templateHeight <= 0)
        {
            return element;
        }

        float ResolvePercentage(float value, int dimension)
        {
            if (value is >= 0 and <= 100)
            {
                return value * dimension / 100f;
            }

            return value;
        }

        return element with
        {
            Left = ResolvePercentage(element.Left, templateWidth),
            Top = ResolvePercentage(element.Top, templateHeight),
            Width = ResolvePercentage(element.Width, templateWidth),
            Height = ResolvePercentage(element.Height, templateHeight),
            X1 = ResolvePercentage(element.X1, templateWidth),
            Y1 = ResolvePercentage(element.Y1, templateHeight),
            X2 = ResolvePercentage(element.X2, templateWidth),
            Y2 = ResolvePercentage(element.Y2, templateHeight),
        };
    }

    private static void DrawContainerElement(
        SKCanvas canvas,
        SubmitRenderJobTemplateElementRequest element,
        IReadOnlyDictionary<string, string> data,
        bool strictBindings,
        IReadOnlyDictionary<string, string> tenantFontPaths,
        int templateWidth,
        int templateHeight)
    {
        float width = Math.Max(1, element.Width);
        float height = Math.Max(1, element.Height);
        SKRect rect = new(element.Left, element.Top, element.Left + width, element.Top + height);

        // Draw background
        if (!string.IsNullOrWhiteSpace(element.BackgroundColor))
        {
            using SKPaint fillPaint = new()
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = ParseColorOrDefault(element.BackgroundColor, SKColors.White),
            };
            canvas.DrawRect(rect, fillPaint);
        }

        // Parse padding: "top,right,bottom,left" or single value
        float paddingTop = 0;
        float paddingLeft = 0;
        if (!string.IsNullOrWhiteSpace(element.Padding))
        {
            string[] parts = element.Padding.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length == 1 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float uniformPadding))
            {
                paddingTop = uniformPadding;
                paddingLeft = uniformPadding;
            }
            else if (parts.Length == 4)
            {
                _ = float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out paddingTop);
                _ = float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out paddingLeft);
            }
        }

        // Clip to container bounds and render children with offset
        canvas.Save();
        canvas.ClipRect(rect);

        foreach (SubmitRenderJobTemplateElementRequest child in element.Children)
        {
            SubmitRenderJobTemplateElementRequest offsetChild = child with
            {
                Left = child.Left + element.Left + paddingLeft,
                Top = child.Top + element.Top + paddingTop,
            };

            DrawTemplateElement(canvas, offsetChild, data, strictBindings, tenantFontPaths, templateWidth, templateHeight);
        }

        canvas.Restore();
    }

    private static void DrawBadgeElement(SKCanvas canvas, SubmitRenderJobTemplateElementRequest element)
    {
        float width = Math.Max(1, element.Width);
        float height = Math.Max(1, element.Height);
        SKColor backgroundColor = ParseColorOrDefault(element.BackgroundColor, SKColors.Red);
        SKColor foregroundColor = ParseColorOrDefault(element.ForegroundColor, SKColors.White);

        using SKPaint fillPaint = new()
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = backgroundColor,
        };

        string badgeStyle = element.BadgeStyle.Trim().ToLowerInvariant();

        if (badgeStyle is "pill" or "capsule")
        {
            float radius = height / 2f;
            SKRect rect = new(element.Left, element.Top, element.Left + width, element.Top + height);
            canvas.DrawRoundRect(rect, radius, radius, fillPaint);
        }
        else if (badgeStyle == "circle")
        {
            float radius = Math.Min(width, height) / 2f;
            float centerX = element.Left + (width / 2f);
            float centerY = element.Top + (height / 2f);
            canvas.DrawCircle(centerX, centerY, radius, fillPaint);
        }
        else if (badgeStyle is "ribbon-top-right" or "ribbon-top-left")
        {
            bool isLeft = badgeStyle == "ribbon-top-left";
            SKPath path = new();
            float tipX = isLeft ? element.Left : element.Left + width;
            float tipY = element.Top + (height / 2f);

            path.MoveTo(element.Left, element.Top);
            path.LineTo(element.Left + width, element.Top);
            path.LineTo(tipX, tipY);
            path.Close();

            canvas.DrawPath(path, fillPaint);
        }
        else
        {
            // Default: rectangle with rounded corners
            float cornerRadius = Math.Min(width, height) * 0.15f;
            SKRect rect = new(element.Left, element.Top, element.Left + width, element.Top + height);
            canvas.DrawRoundRect(rect, cornerRadius, cornerRadius, fillPaint);
        }

        // Draw badge text centered
        if (!string.IsNullOrWhiteSpace(element.Value))
        {
            float fontSize = Math.Min(height * 0.6f, width / (element.Value.Length * 0.6f));
            fontSize = Math.Max(8, fontSize);

            using SKFont font = new(SKTypeface.Default, fontSize);
            using SKPaint textPaint = new()
            {
                IsAntialias = true,
                Color = foregroundColor,
            };

            float textX = element.Left + (width / 2f);
            float textY = element.Top + (height / 2f) + (fontSize * 0.35f);

            canvas.DrawText(element.Value, textX, textY, SKTextAlign.Center, font, textPaint);
        }
    }

    private static void DrawPriceElement(
        SKCanvas canvas,
        SubmitRenderJobTemplateElementRequest element,
        IReadOnlyDictionary<string, string> data,
        bool strictBindings)
    {
        string currency = !string.IsNullOrWhiteSpace(element.Currency) ? element.Currency : "";
        string wasPrice = ResolveValue(element with { Binding = string.Empty, Value = element.WasPrice }, data, strictBindings);
        string nowPrice = ResolveValue(element with { Binding = string.Empty, Value = element.NowPrice }, data, strictBindings);

        if (string.IsNullOrWhiteSpace(nowPrice) && string.IsNullOrWhiteSpace(wasPrice))
        {
            return;
        }

        float width = Math.Max(1, element.Width);
        float height = Math.Max(1, element.Height);
        SKColor foregroundColor = ParseColorOrDefault(element.ForegroundColor, SKColors.Black);

        // Auto-calculate font sizes
        float nowFontSize = element.FontSize > 0 ? element.FontSize : 24;
        float wasFontSize = nowFontSize * 0.7f;

        if (element.AutoSize && width > 0 && height > 0)
        {
            string nowText = currency + nowPrice;
            nowFontSize = ResolveAutoFontSize(nowText, SKTypeface.Default, element with { FontSize = nowFontSize });
        }

        float nowTextY = element.Top + (height * 0.55f);
        float wasTextY = element.Top + (height * 0.85f);

        // Draw "now" price (larger, prominent)
        if (!string.IsNullOrWhiteSpace(nowPrice))
        {
            string nowText = currency + nowPrice;
            using SKFont nowFont = new(SKTypeface.Default, nowFontSize);
            using SKPaint nowPaint = new()
            {
                IsAntialias = true,
                Color = foregroundColor,
                Style = SKPaintStyle.Fill,
            };

            (float anchorX, SKTextAlign align) = ResolveHorizontalAlignment(element);
            canvas.DrawText(nowText, anchorX, nowTextY, align, nowFont, nowPaint);
        }

        // Draw "was" price (smaller, strikethrough)
        if (!string.IsNullOrWhiteSpace(wasPrice))
        {
            string wasText = currency + wasPrice;
            using SKFont wasFont = new(SKTypeface.Default, wasFontSize);
            using SKPaint wasPaint = new()
            {
                IsAntialias = true,
                Color = ParseColorOrDefault("#888888", foregroundColor),
                Style = SKPaintStyle.Fill,
            };

            (float anchorX, SKTextAlign align) = ResolveHorizontalAlignment(element);
            canvas.DrawText(wasText, anchorX, wasTextY, align, wasFont, wasPaint);

            // Strikethrough line
            float lineWidth = wasFont.MeasureText(wasText);
            float lineX = align switch
            {
                SKTextAlign.Center => anchorX - (lineWidth / 2f),
                SKTextAlign.Right => anchorX - lineWidth,
                _ => anchorX,
            };
            float strikeY = wasTextY - (wasFontSize * 0.3f);

            using SKPaint strikePaint = new()
            {
                IsAntialias = true,
                Color = wasPaint.Color,
                StrokeWidth = Math.Max(1, wasFontSize / 8f),
            };
            canvas.DrawLine(lineX, strikeY, lineX + lineWidth, strikeY, strikePaint);
        }
    }

    private static (float Left, float Top, float Width, float Height) ResolveCompiledPercentages(
        CompiledElement element, int templateWidth, int templateHeight)
    {
        if (templateWidth <= 0 || templateHeight <= 0)
        {
            return (element.Left, element.Top, element.Width, element.Height);
        }

        float Resolve(float value, int dimension) => value is >= 0 and <= 100 ? value * dimension / 100f : value;

        return (
            Resolve(element.Left, templateWidth),
            Resolve(element.Top, templateHeight),
            Resolve(element.Width, templateWidth),
            Resolve(element.Height, templateHeight));
    }

    private static void DrawCompiledTextElement(
        SKCanvas canvas, CompiledElement element, IReadOnlyDictionary<string, string> data, bool strictBindings, IReadOnlyDictionary<string, string> tenantFontPaths)
    {
        string value = ResolveCompiledValue(element, data, strictBindings);
        if (string.IsNullOrWhiteSpace(value)) return;

        SKTypeface typeface = ResolveTypeface(element.FontFamily, element.FontWeight, tenantFontPaths);
        float fontSize = element.FontSize > 0 ? element.FontSize : DefaultFontSize;

        if (element.AutoSize && element.Width > 0 && element.Height > 0)
        {
            fontSize = ResolveAutoFontSize(
                value,
                typeface,
                new SubmitRenderJobTemplateElementRequest
                {
                    Width = element.Width,
                    Height = element.Height,
                    MinFontSize = element.MinFontSize,
                    MaxFontSize = element.MaxFontSize,
                    WordWrap = element.WordWrap,
                    LineHeight = element.LineHeight,
                });
        }

        float lineHeightPx = fontSize * Math.Max(0.1f, element.LineHeight);
        (float anchorX, SKTextAlign align) = ResolveCompiledHorizontalAlignment(element);

        using SKFont font = new(typeface, fontSize);
        SKColor foregroundColor = element.ForegroundColor;

        string effectiveDirection = ResolveTextDirection(value, element.TextDirection);
        bool isRtl = effectiveDirection == "rtl";

        IReadOnlyList<string> lines = element.WordWrap && element.Width > 0
            ? WrapText(value, font, element.Width, element.MaxLines, element.Ellipsis)
            : [value];

        float totalTextHeight = lines.Count * lineHeightPx;
        float startY = element.Height > 0
            ? element.Top + ((element.Height - totalTextHeight) / 2f) + (fontSize * 0.8f)
            : element.Top + fontSize;

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                startY += lineHeightPx;
                continue;
            }

            string drawLine = isRtl ? new string(line.Reverse().ToArray()) : line;
            SKTextAlign drawAlign = isRtl ? SwapAlignment(align) : align;
            float drawAnchorX = isRtl ? SwapAnchorX(anchorX, align, element) : anchorX;
            if (element.TextEffect == "shadow")
            {
                using SKPaint shadowPaint = new() { IsAntialias = true, Color = new SKColor(0, 0, 0, 80), Style = SKPaintStyle.Fill };
                canvas.DrawText(drawLine, drawAnchorX + 1, startY + 1, drawAlign, font, shadowPaint);
            }

            if (element.TextEffect == "outline")
            {
                using SKPaint outlinePaint = new() { IsAntialias = true, Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(1, fontSize / 8f) };
                canvas.DrawText(drawLine, drawAnchorX, startY, drawAlign, font, outlinePaint);
            }

            using (SKPaint paint = new() { IsAntialias = true, Color = foregroundColor })
            {
                canvas.DrawText(drawLine, drawAnchorX, startY, drawAlign, font, paint);
            }

            if (element.TextEffect == "strikethrough")
            {
                float lineWidth = font.MeasureText(drawLine);
                float lineX = drawAlign switch
                {
                    SKTextAlign.Center => drawAnchorX - (lineWidth / 2f),
                    SKTextAlign.Right => drawAnchorX - lineWidth,
                    _ => drawAnchorX,
                };
                using SKPaint strikePaint = new() { IsAntialias = true, Color = foregroundColor, StrokeWidth = Math.Max(1, fontSize / 10f) };
                canvas.DrawLine(lineX, startY - (fontSize * 0.3f), lineX + lineWidth, startY - (fontSize * 0.3f), strikePaint);
            }

            startY += lineHeightPx;
        }
    }

    private static void DrawCompiledCodeElement(SKCanvas canvas, CompiledElement element, IReadOnlyDictionary<string, string> data, bool strictBindings)
    {
        string value = ResolveCompiledValue(element, data, strictBindings);
        if (string.IsNullOrWhiteSpace(value)) return;

        int width = (int)MathF.Round(element.Width <= 0 ? DefaultBarcodeSize : element.Width);
        int height = (int)MathF.Round(element.Height <= 0 ? DefaultBarcodeSize : element.Height);
        BarcodeFormat format = ResolveBarcodeFormat(element.NormalizedType, element.Format);

        using SKBitmap barcodeBitmap = CreateBarcode(format, value, Math.Max(1, width), Math.Max(1, height));
        canvas.DrawBitmap(barcodeBitmap, new SKRect(element.Left, element.Top, element.Left + width, element.Top + height));

        if (element.ShowValue && !string.IsNullOrWhiteSpace(value))
        {
            float textY = element.Top + height + 4;
            float textFontSize = Math.Max(8, Math.Min(14, width / (value.Length * 0.6f)));
            using SKFont textFont = new(SKTypeface.Default, textFontSize);
            using SKPaint textPaint = new() { IsAntialias = true, Color = element.ForegroundColor };
            canvas.DrawText(value, element.Left + (width / 2f), textY, SKTextAlign.Center, textFont, textPaint);
        }
    }

    private static void DrawCompiledRectangleElement(SKCanvas canvas, CompiledElement element)
    {
        float width = Math.Max(1, element.Width);
        float height = Math.Max(1, element.Height);
        SKRect rect = new(element.Left, element.Top, element.Left + width, element.Top + height);
        float cornerRadius = Math.Max(0, element.CornerRadius);

        using SKShader? gradient = CreateGradientShader(element, rect);

        if (gradient is not null || element.Fill || element.BackgroundColor.Alpha > 0)
        {
            using SKPaint fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = element.BackgroundColor };
            if (gradient is not null)
            {
                fillPaint.Shader = gradient;
            }

            DrawRoundedRect(canvas, rect, cornerRadius, fillPaint);
        }

        float strokeWidth;
        if (element.StrokeWidth > 0)
        {
            strokeWidth = element.StrokeWidth;
        }
        else
        {
            strokeWidth = element.Fill ? 0 : 1;
        }

        if (strokeWidth > 0)
        {
            using SKPaint strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = strokeWidth, Color = element.ForegroundColor };
            DrawRoundedRect(canvas, rect, cornerRadius, strokePaint);
        }
    }

    private static void DrawCompiledCircleElement(SKCanvas canvas, CompiledElement element)
    {
        float width = Math.Max(1, element.Width);
        float height = Math.Max(1, element.Height);
        float centerX = element.Left + (width / 2f);
        float centerY = element.Top + (height / 2f);
        float radiusX = width / 2f;
        float radiusY = height / 2f;
        SKRect bounds = new(element.Left, element.Top, element.Left + width, element.Top + height);
        using SKShader? gradient = CreateGradientShader(element, bounds);

        if (gradient is not null || element.Fill || element.BackgroundColor.Alpha > 0)
        {
            using SKPaint fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = element.BackgroundColor };
            if (gradient is not null)
            {
                fillPaint.Shader = gradient;
            }

            if (Math.Abs(radiusX - radiusY) < 0.5f)
            {
                canvas.DrawCircle(centerX, centerY, radiusX, fillPaint);
            }
            else
            {
                canvas.DrawOval(bounds, fillPaint);
            }
        }

        float strokeWidth;
        if (element.StrokeWidth > 0)
        {
            strokeWidth = element.StrokeWidth;
        }
        else
        {
            strokeWidth = element.Fill ? 0 : 1;
        }

        if (strokeWidth > 0)
        {
            using SKPaint strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = strokeWidth, Color = element.ForegroundColor };
            if (Math.Abs(radiusX - radiusY) < 0.5f)
            {
                canvas.DrawCircle(centerX, centerY, radiusX, strokePaint);
            }
            else
            {
                canvas.DrawOval(bounds, strokePaint);
            }
        }
    }

    private static void DrawCompiledImageElement(SKCanvas canvas, CompiledElement element, IReadOnlyDictionary<string, string> data, bool strictBindings)
    {
        string value = ResolveCompiledValue(element, data, strictBindings);
        if (string.IsNullOrWhiteSpace(value) || !File.Exists(value.Trim())) return;

        using SKBitmap? bitmap = SKBitmap.Decode(value.Trim());
        if (bitmap is null) return;

        SKRect destRect = new(element.Left, element.Top, element.Left + Math.Max(1, element.Width), element.Top + Math.Max(1, element.Height));
        canvas.DrawBitmap(bitmap, destRect);
    }

    private static void DrawCompiledLineElement(SKCanvas canvas, CompiledElement element)
    {
        using SKPaint paint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(1, element.StrokeWidth), Color = element.ForegroundColor };
        canvas.DrawLine(element.X1, element.Y1, element.X2, element.Y2, paint);
    }

    private static void DrawCompiledContainerElement(
        SKCanvas canvas,
        CompiledElement element,
        IReadOnlyDictionary<string, string> data,
        bool strictBindings,
        IReadOnlyDictionary<string, string> tenantFontPaths,
        int templateWidth,
        int templateHeight)
    {
        float width = Math.Max(1, element.Width);
        float height = Math.Max(1, element.Height);
        SKRect rect = new(element.Left, element.Top, element.Left + width, element.Top + height);

        if (element.BackgroundColor.Alpha > 0)
        {
            using SKPaint fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = element.BackgroundColor };
            canvas.DrawRect(rect, fillPaint);
        }

        float padTop = 0;
        float padLeft = 0;
        if (!string.IsNullOrWhiteSpace(element.Padding))
        {
            string[] parts = element.Padding.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length == 1 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float uniform))
            {
                padTop = uniform;
                padLeft = uniform;
            }
            else if (parts.Length == 4)
            {
                _ = float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out padTop);
                _ = float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out padLeft);
            }
        }

        canvas.Save();
        canvas.ClipRect(rect);

        foreach (CompiledElement child in element.Children)
        {
            CompiledElement offsetChild = child with
            {
                Left = child.Left + element.Left + padLeft,
                Top = child.Top + element.Top + padTop,
            };
            DrawCompiledElement(canvas, offsetChild, data, strictBindings, tenantFontPaths, templateWidth, templateHeight);
        }

        canvas.Restore();
    }

    private static void DrawCompiledBadgeElement(SKCanvas canvas, CompiledElement element)
    {
        float width = Math.Max(1, element.Width);
        float height = Math.Max(1, element.Height);
        SKColor bg = element.BackgroundColor.Alpha > 0 ? element.BackgroundColor : SKColors.Red;
        SKColor fg = element.ForegroundColor;

        using SKPaint fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = bg };

        if (element.BadgeStyle is "pill" or "capsule")
        {
            float cornerRadius = height / 2f;
            canvas.DrawRoundRect(new SKRect(element.Left, element.Top, element.Left + width, element.Top + height), cornerRadius, cornerRadius, fillPaint);
        }
        else if (element.BadgeStyle == "circle")
        {
            float radius = Math.Min(width, height) / 2f;
            canvas.DrawCircle(element.Left + (width / 2f), element.Top + (height / 2f), radius, fillPaint);
        }
        else if (element.BadgeStyle is "ribbon-top-right" or "ribbon-top-left")
        {
            bool left = element.BadgeStyle == "ribbon-top-left";
            using SKPath path = new();
            path.MoveTo(element.Left, element.Top);
            path.LineTo(element.Left + width, element.Top);
            path.LineTo(left ? element.Left : element.Left + width, element.Top + (height / 2f));
            path.Close();
            canvas.DrawPath(path, fillPaint);
        }
        else
        {
            float cr = Math.Min(width, height) * 0.15f;
            canvas.DrawRoundRect(new SKRect(element.Left, element.Top, element.Left + width, element.Top + height), cr, cr, fillPaint);
        }

        if (!string.IsNullOrWhiteSpace(element.Value))
        {
            float fs = Math.Max(8, Math.Min(height * 0.6f, width / (element.Value.Length * 0.6f)));
            using SKFont font = new(SKTypeface.Default, fs);
            using SKPaint tp = new() { IsAntialias = true, Color = fg };
            canvas.DrawText(element.Value, element.Left + (width / 2f), element.Top + (height / 2f) + (fs * 0.35f), SKTextAlign.Center, font, tp);
        }
    }

    private static void DrawCompiledPriceElement(
        SKCanvas canvas, CompiledElement element, IReadOnlyDictionary<string, string> data, bool strictBindings)
    {
        string currency = element.Currency;
        string wasPrice = ResolveCompiledValue(element with { Binding = string.Empty, Value = element.WasPrice }, data, strictBindings);
        string nowPrice = ResolveCompiledValue(element with { Binding = string.Empty, Value = element.NowPrice }, data, strictBindings);

        if (string.IsNullOrWhiteSpace(nowPrice) && string.IsNullOrWhiteSpace(wasPrice))
        {
            return;
        }

        float width = Math.Max(1, element.Width);
        float height = Math.Max(1, element.Height);
        SKColor fg = element.ForegroundColor;

        float nowFontSize = element.FontSize > 0 ? element.FontSize : 24;
        float wasFontSize = nowFontSize * 0.7f;

        if (element.AutoSize && width > 0 && height > 0)
        {
            string nowText = currency + nowPrice;
            nowFontSize = ResolveAutoFontSize(
                nowText,
                SKTypeface.Default,
                new SubmitRenderJobTemplateElementRequest
                {
                    Width = width,
                    Height = height,
                    FontSize = nowFontSize,
                });
        }

        float nowY = element.Top + (height * 0.55f);
        float wasY = element.Top + (height * 0.85f);

        if (!string.IsNullOrWhiteSpace(nowPrice))
        {
            string nt = currency + nowPrice;
            using SKFont font = new(SKTypeface.Default, nowFontSize);
            using SKPaint paint = new() { IsAntialias = true, Color = fg, Style = SKPaintStyle.Fill };
            (float ax, SKTextAlign al) = ResolveCompiledHorizontalAlignment(element);
            canvas.DrawText(nt, ax, nowY, al, font, paint);
        }

        if (!string.IsNullOrWhiteSpace(wasPrice))
        {
            string wt = currency + wasPrice;
            using SKFont font = new(SKTypeface.Default, wasFontSize);
            using SKPaint paint = new() { IsAntialias = true, Color = ParseColorOrDefault("#888888", fg), Style = SKPaintStyle.Fill };
            (float ax, SKTextAlign al) = ResolveCompiledHorizontalAlignment(element);
            canvas.DrawText(wt, ax, wasY, al, font, paint);

            float lw = font.MeasureText(wt);
            float lx = al switch
            {
                SKTextAlign.Center => ax - (lw / 2f),
                SKTextAlign.Right => ax - lw,
                _ => ax,
            };
            using SKPaint sp = new() { IsAntialias = true, Color = paint.Color, StrokeWidth = Math.Max(1, wasFontSize / 8f) };
            canvas.DrawLine(lx, wasY - (wasFontSize * 0.3f), lx + lw, wasY - (wasFontSize * 0.3f), sp);
        }
    }

    private static string ResolveCompiledValue(CompiledElement element, IReadOnlyDictionary<string, string> data, bool strictBindings)
    {
        if (!string.IsNullOrWhiteSpace(element.Binding))
        {
            string key = element.Binding.Trim();
            if (data.TryGetValue(key, out string? boundValue))
            {
                return boundValue ?? string.Empty;
            }

            if (strictBindings)
            {
                throw new InvalidOperationException($"Missing binding key '{key}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(element.Value))
        {
            return string.Empty;
        }

        string resolved = element.Value;
        foreach ((string templateKey, string templateValue) in data)
        {
            resolved = resolved.Replace($"{{{{{templateKey}}}}}", templateValue ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        if (strictBindings && resolved.Contains("{{", StringComparison.Ordinal) && resolved.Contains("}}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unresolved template token in value '{element.Value}'.");
        }

        return resolved;
    }

    private static (float AnchorX, SKTextAlign Align) ResolveCompiledHorizontalAlignment(CompiledElement element)
    {
        if (element.HorizontalAlign.Equals("center", StringComparison.OrdinalIgnoreCase) && element.Width > 0)
        {
            return (element.Left + (element.Width / 2f), SKTextAlign.Center);
        }

        if (element.HorizontalAlign.Equals("right", StringComparison.OrdinalIgnoreCase) && element.Width > 0)
        {
            return (element.Left + element.Width, SKTextAlign.Right);
        }

        return (element.Left, SKTextAlign.Left);
    }

    private static SKColor ParseColorOrDefault(string color, SKColor fallback)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return fallback;
        }

        string normalized = color.Trim();
        if (normalized.Length == 7 && normalized[0] == '#')
        {
            if (TryParseHexByte(normalized.AsSpan(1, 2), out byte red)
                && TryParseHexByte(normalized.AsSpan(3, 2), out byte green)
                && TryParseHexByte(normalized.AsSpan(5, 2), out byte blue))
            {
                return new SKColor(red, green, blue, 255);
            }

            return fallback;
        }

        if (normalized.Length == 9 && normalized[0] == '#')
        {
            if (TryParseHexByte(normalized.AsSpan(1, 2), out byte alpha)
                && TryParseHexByte(normalized.AsSpan(3, 2), out byte red)
                && TryParseHexByte(normalized.AsSpan(5, 2), out byte green)
                && TryParseHexByte(normalized.AsSpan(7, 2), out byte blue))
            {
                return new SKColor(red, green, blue, alpha);
            }

            return fallback;
        }

        return fallback;
    }

    private static bool TryParseHexByte(ReadOnlySpan<char> value, out byte parsed)
    {
        return byte.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed);
    }

    private static void WriteWithMagick(
        SKImage image,
        Stream output,
        MagickFormat magickFormat,
        int quality,
        IReadOnlyList<string> paletteColors,
        int cancellationCheckStridePixels,
        CancellationToken cancellationToken)
    {
        (byte[] pixels, int width, int height) = ReadRgbaPixels(image);
        ApplyDisplayPaletteInPlace(pixels, paletteColors, cancellationCheckStridePixels, cancellationToken);

        MagickReadSettings readSettings = new()
        {
            Width = (uint)width,
            Height = (uint)height,
            Format = MagickFormat.Rgba,
        };

        using MagickImage magickImage = new(pixels, readSettings);
        ApplyEinkProfile(magickImage, paletteColors.Count > 0);
        magickImage.Format = magickFormat;

        if (magickFormat is MagickFormat.Jpeg or MagickFormat.WebP)
        {
            magickImage.Quality = (uint)quality;
        }

        magickImage.Write(output);
    }

    private static void ApplyEinkProfile(MagickImage magickImage, bool hasCustomPalette)
    {
        if (hasCustomPalette)
        {
            return;
        }

        // E-ink displays perform best with grayscale palettes and reduced levels.
        magickImage.ColorSpace = ColorSpace.Gray;
        magickImage.ColorType = ColorType.Grayscale;
        magickImage.Depth = 8;

        QuantizeSettings quantizeSettings = new()
        {
            Colors = 16,
            ColorSpace = ColorSpace.Gray,
            DitherMethod = DitherMethod.FloydSteinberg,
        };

        magickImage.Quantize(quantizeSettings);
    }

    private static void ApplyDisplayPaletteInPlace(
        byte[] pixels,
        IReadOnlyList<string> paletteColors,
        int cancellationCheckStridePixels,
        CancellationToken cancellationToken)
    {
        if (paletteColors.Count == 0)
        {
            return;
        }

        IReadOnlyList<RgbColor> parsedPalette = ParsePaletteColors(paletteColors);
        RgbColor backgroundColor = FindNearestColor(255, 255, 255, parsedPalette);

        int stride = Math.Max(1, cancellationCheckStridePixels);
        int processedPixels = 0;

        for (int index = 0; index < pixels.Length; index += 4)
        {
            if ((processedPixels++ % stride) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            byte alpha = pixels[index + 3];
            if (alpha < 16)
            {
                // Transparent source pixels should still resolve to a valid display color.
                pixels[index] = backgroundColor.Red;
                pixels[index + 1] = backgroundColor.Green;
                pixels[index + 2] = backgroundColor.Blue;
                pixels[index + 3] = 255;
                continue;
            }

            RgbColor nearest = FindNearestColor(
                pixels[index],
                pixels[index + 1],
                pixels[index + 2],
                parsedPalette);

            pixels[index] = nearest.Red;
            pixels[index + 1] = nearest.Green;
            pixels[index + 2] = nearest.Blue;
            pixels[index + 3] = 255;
        }
    }

    /// <summary>
    /// Quantizes image pixels to the provider's supported colors in-place on a copy.
    /// Returns a new SKImage with quantized pixels.
    /// </summary>
    private static SKImage ApplyProviderPalette(SKImage image, IReadOnlyList<string> supportedColors)
    {
        if (supportedColors.Count == 0)
        {
            return image;
        }

        int width = image.Width;
        int height = image.Height;

        SKImageInfo info = new(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        byte[] pixels = new byte[width * height * 4];
        GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            image.ReadPixels(info, handle.AddrOfPinnedObject(), width * 4, 0, 0);
        }
        finally
        {
            handle.Free();
        }

        IReadOnlyList<RgbColor> palette = ParsePaletteColors(supportedColors);
        RgbColor backgroundColor = FindNearestColor(255, 255, 255, palette);

        for (int index = 0; index < pixels.Length; index += 4)
        {
            byte alpha = pixels[index + 3];
            if (alpha < 16)
            {
                pixels[index] = backgroundColor.Red;
                pixels[index + 1] = backgroundColor.Green;
                pixels[index + 2] = backgroundColor.Blue;
                pixels[index + 3] = 255;
                continue;
            }

            RgbColor nearest = FindNearestColor(pixels[index], pixels[index + 1], pixels[index + 2], palette);
            pixels[index] = nearest.Red;
            pixels[index + 1] = nearest.Green;
            pixels[index + 2] = nearest.Blue;
            pixels[index + 3] = 255;
        }

        using SKBitmap quantizedBitmap = new(info);
        GCHandle writeHandle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            quantizedBitmap.SetPixels(writeHandle.AddrOfPinnedObject());
        }
        finally
        {
            writeHandle.Free();
        }

        return SKImage.FromBitmap(quantizedBitmap) ?? image;
    }

    private static IReadOnlyList<RgbColor> ParsePaletteColors(IReadOnlyList<string> paletteColors)
    {
        string paletteKey = string.Join('|', paletteColors.Select(static color => color.Trim().ToUpperInvariant()));

        return PaletteCache.GetOrAdd(
            paletteKey,
            _ =>
            {
                RgbColor[] parsed = new RgbColor[paletteColors.Count];

                for (int index = 0; index < paletteColors.Count; index++)
                {
                    string normalized = paletteColors[index].Trim();

                    if (normalized.Length == 7
                        && normalized[0] == '#'
                        && TryParseHexByte(normalized.AsSpan(1, 2), out byte red)
                        && TryParseHexByte(normalized.AsSpan(3, 2), out byte green)
                        && TryParseHexByte(normalized.AsSpan(5, 2), out byte blue))
                    {
                        parsed[index] = new RgbColor(red, green, blue);
                        continue;
                    }

                    if (normalized.Length == 9
                        && normalized[0] == '#'
                        && TryParseHexByte(normalized.AsSpan(3, 2), out byte argbRed)
                        && TryParseHexByte(normalized.AsSpan(5, 2), out byte argbGreen)
                        && TryParseHexByte(normalized.AsSpan(7, 2), out byte argbBlue))
                    {
                        parsed[index] = new RgbColor(argbRed, argbGreen, argbBlue);
                        continue;
                    }

                    throw new InvalidOperationException($"Invalid palette color '{paletteColors[index]}'.");
                }

                return parsed;
            });
    }

    private static RgbColor FindNearestColor(byte red, byte green, byte blue, IReadOnlyList<RgbColor> palette)
    {
        RgbColor nearestColor = palette[0];
        int nearestDistance = int.MaxValue;

        foreach (RgbColor paletteColor in palette)
        {
            int redDelta = red - paletteColor.Red;
            int greenDelta = green - paletteColor.Green;
            int blueDelta = blue - paletteColor.Blue;
            int distance = (redDelta * redDelta) + (greenDelta * greenDelta) + (blueDelta * blueDelta);

            if (distance == 0)
            {
                return paletteColor;
            }

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestColor = paletteColor;
            }
        }

        return nearestColor;
    }

    private static (byte[] Pixels, int Width, int Height) ReadRgbaPixels(SKImage image)
    {
        int width = image.Width;
        int height = image.Height;

        SKImageInfo info = new(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        int rowBytes = info.RowBytes;
        byte[] pixels = new byte[rowBytes * info.Height];

        GCHandle pinnedPixels = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            bool readSucceeded = image.ReadPixels(
                info,
                pinnedPixels.AddrOfPinnedObject(),
                rowBytes,
                0,
                0,
                SKImageCachingHint.Allow);

            if (!readSucceeded)
            {
                throw new InvalidOperationException("Unable to read image pixels for output encoding.");
            }
        }
        finally
        {
            pinnedPixels.Free();
        }

        return (pixels, width, height);
    }

    private static (MagickFormat MagickFormat, string Extension, int Quality) ResolveOutputType(string outputType)
    {
        if (outputType.Equals("jpeg", StringComparison.OrdinalIgnoreCase)
            || outputType.Equals("jpg", StringComparison.OrdinalIgnoreCase))
        {
            return (MagickFormat.Jpeg, "jpg", 90);
        }

        if (outputType.Equals("webp", StringComparison.OrdinalIgnoreCase))
        {
            return (MagickFormat.WebP, "webp", 90);
        }

        if (outputType.Equals("pam", StringComparison.OrdinalIgnoreCase))
        {
            return (MagickFormat.Pam, "pam", 100);
        }

        return (MagickFormat.Png, "png", 100);
    }

    private static string NormalizeOutputExtension(string outputTypeOrExtension)
    {
        (_, string extension, _) = ResolveOutputType(outputTypeOrExtension);
        return extension;
    }

    private static BarcodeFormat ResolveBarcodeFormat(string type, string formatHint)
    {
        string normalized = string.IsNullOrWhiteSpace(formatHint)
            ? type
            : formatHint;

        if (normalized.Contains("datamatrix", StringComparison.OrdinalIgnoreCase))
        {
            return BarcodeFormat.DATA_MATRIX;
        }

        if (normalized.Contains("ean13", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("ean-13", StringComparison.OrdinalIgnoreCase))
        {
            return BarcodeFormat.EAN_13;
        }

        if (normalized.Contains("upca", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("upc-a", StringComparison.OrdinalIgnoreCase))
        {
            return BarcodeFormat.UPC_A;
        }

        if (normalized.Contains("code39", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("code_39", StringComparison.OrdinalIgnoreCase))
        {
            return BarcodeFormat.CODE_39;
        }

        if (normalized.Contains("itf", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("interleaved", StringComparison.OrdinalIgnoreCase))
        {
            return BarcodeFormat.ITF;
        }

        if (normalized.Contains("aztec", StringComparison.OrdinalIgnoreCase))
        {
            return BarcodeFormat.AZTEC;
        }

        if (normalized.Contains("barcode", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("code128", StringComparison.OrdinalIgnoreCase))
        {
            return BarcodeFormat.CODE_128;
        }

        if (normalized.Contains("pdf417", StringComparison.OrdinalIgnoreCase))
        {
            return BarcodeFormat.PDF_417;
        }

        return BarcodeFormat.QR_CODE;
    }

    private static SKBitmap CreateBarcode(BarcodeFormat format, string content, int width, int height)
    {
        var writer = new BarcodeWriter
        {
            Format = format,
            Options = new EncodingOptions
            {
                Width = width,
                Height = height,
                Margin = 1,
                PureBarcode = true,
            },
            Renderer = new SKBitmapRenderer(),
        };

        return writer.Write(content);
    }
}
