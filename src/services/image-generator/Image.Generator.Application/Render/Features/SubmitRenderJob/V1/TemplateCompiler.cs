// <copyright file="TemplateCompiler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Globalization;
using SkiaSharp;

namespace Image.Generator.Application.Render.Features.SubmitRenderJob.V1;

/// <summary>
/// Pre-compiled template that avoids repeated parsing and object allocation per render.
/// </summary>
internal sealed record CompiledTemplate
{
    public required SubmitRenderJobTemplateRequest OriginalTemplate { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }

    public required SKColor BackgroundColor { get; init; }

    public required bool UsePercentagePositioning { get; init; }

    public required IReadOnlyList<CompiledElement> Elements { get; init; }
}

/// <summary>
/// Pre-compiled element with resolved type, colors, and render hints.
/// </summary>
internal sealed record CompiledElement
{
    public required string NormalizedType { get; init; }

    public required float Left { get; init; }

    public required float Top { get; init; }

    public required float Width { get; init; }

    public required float Height { get; init; }

    public required SKColor ForegroundColor { get; init; }

    public required SKColor BackgroundColor { get; init; }

    public required float StrokeWidth { get; init; }

    public required float CornerRadius { get; init; }

    public required bool Fill { get; init; }

    // Text-specific compiled state
    public required string FontFamily { get; init; }

    public required float FontSize { get; init; }

    public required string FontWeight { get; init; }

    public required string HorizontalAlign { get; init; }

    public required bool WordWrap { get; init; }

    public required int MaxLines { get; init; }

    public required float LineHeight { get; init; }

    public required string Ellipsis { get; init; }

    public required bool AutoSize { get; init; }

    public required float MinFontSize { get; init; }

    public required float MaxFontSize { get; init; }

    public required string TextEffect { get; init; }
    public required string TextDirection { get; init; }

    // Gradient-specific
    public required string GradientType { get; init; }
    public required IReadOnlyList<SKColor> GradientColors { get; init; }
    public required float GradientAngle { get; init; }
    public required float GradientStartX { get; init; }
    public required float GradientStartY { get; init; }
    public required float GradientEndX { get; init; }
    public required float GradientEndY { get; init; }

    // Template inheritance
    public required string ElementId { get; init; }

    // Barcode-specific
    public required bool ShowValue { get; init; }

    // Line-specific

    // Line-specific
    public required float X1 { get; init; }

    public required float Y1 { get; init; }

    public required float X2 { get; init; }

    public required float Y2 { get; init; }

    // Container-specific
    public required IReadOnlyList<CompiledElement> Children { get; init; }

    public required string Padding { get; init; }

    // Badge-specific
    public required string BadgeStyle { get; init; }

    // Price-specific
    public required string WasPrice { get; init; }

    public required string NowPrice { get; init; }

    public required string Currency { get; init; }

    // Original mutable properties for binding resolution
    public required string Value { get; init; }

    public required string Binding { get; init; }

    public required string Format { get; init; }
}

/// <summary>
/// Compiles template requests into optimized render structures.
/// </summary>
internal static class TemplateCompiler
{
    public static CompiledTemplate Compile(SubmitRenderJobTemplateRequest template, SubmitRenderJobTemplateRequest? parentTemplate = null)
    {
        int width = Math.Max(1, template.Width);
        int height = Math.Max(1, template.Height);

        IReadOnlyList<SubmitRenderJobTemplateElementRequest> mergedElements = parentTemplate is not null
            ? MergeElements(parentTemplate.Elements, template.Elements)
            : template.Elements.ToList();

        return new CompiledTemplate
        {
            OriginalTemplate = template,
            Width = width,
            Height = height,
            BackgroundColor = ParseColor(template.BackgroundColor, SKColors.White),
            UsePercentagePositioning = template.UsePercentagePositioning,
            Elements = CompileElements(mergedElements),
        };
    }

    /// <summary>
    /// Merges parent elements with child overrides. Child elements with matching ElementId override parent elements.
    /// New elements (no matching ElementId in parent) are appended. Parent elements without child override are preserved.
    /// </summary>
    private static IReadOnlyList<SubmitRenderJobTemplateElementRequest> MergeElements(
        IEnumerable<SubmitRenderJobTemplateElementRequest> parentElements,
        IEnumerable<SubmitRenderJobTemplateElementRequest> childElements)
    {
        Dictionary<string, SubmitRenderJobTemplateElementRequest> merged = new(StringComparer.OrdinalIgnoreCase);

        foreach (SubmitRenderJobTemplateElementRequest element in parentElements.Where(parentElement => !string.IsNullOrWhiteSpace(parentElement.ElementId)))
        {
            merged[element.ElementId] = element;
        }

        foreach (SubmitRenderJobTemplateElementRequest element in childElements)
        {
            if (!string.IsNullOrWhiteSpace(element.ElementId) && merged.TryGetValue(element.ElementId, out SubmitRenderJobTemplateElementRequest? parent))
            {
                // Override: merge child over parent
                merged[element.ElementId] = MergeElement(parent, element);
            }
            else
            {
                // New element or no ElementId
                merged[(element.ElementId, Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)).ToString()] = element;
            }
        }

        return merged.Values.ToList();
    }

    private static SubmitRenderJobTemplateElementRequest MergeElement(
        SubmitRenderJobTemplateElementRequest parent,
        SubmitRenderJobTemplateElementRequest child)
    {
        return new SubmitRenderJobTemplateElementRequest
        {
            ElementId = child.ElementId,
            Type = child.Type.Length > 0 ? child.Type : parent.Type,
            Left = HasMeaningfulFloatValue(child.Left) ? child.Left : parent.Left,
            Top = HasMeaningfulFloatValue(child.Top) ? child.Top : parent.Top,
            Width = HasMeaningfulFloatValue(child.Width) ? child.Width : parent.Width,
            Height = HasMeaningfulFloatValue(child.Height) ? child.Height : parent.Height,
            Value = child.Value.Length > 0 ? child.Value : parent.Value,
            Binding = child.Binding.Length > 0 ? child.Binding : parent.Binding,
            Format = child.Format.Length > 0 ? child.Format : parent.Format,
            FontFamily = child.FontFamily.Length > 0 ? child.FontFamily : parent.FontFamily,
            FontSize = HasMeaningfulFloatValue(child.FontSize) ? child.FontSize : parent.FontSize,
            FontWeight = child.FontWeight.Length > 0 ? child.FontWeight : parent.FontWeight,
            HorizontalAlign = child.HorizontalAlign.Length > 0 ? child.HorizontalAlign : parent.HorizontalAlign,
            ForegroundColor = child.ForegroundColor.Length > 0 ? child.ForegroundColor : parent.ForegroundColor,
            BackgroundColor = child.BackgroundColor.Length > 0 ? child.BackgroundColor : parent.BackgroundColor,
            StrokeWidth = HasMeaningfulFloatValue(child.StrokeWidth) ? child.StrokeWidth : parent.StrokeWidth,
            CornerRadius = HasMeaningfulFloatValue(child.CornerRadius) ? child.CornerRadius : parent.CornerRadius,
            Fill = child.Fill || parent.Fill,
            WordWrap = child.WordWrap || parent.WordWrap,
            MaxLines = child.MaxLines != 0 ? child.MaxLines : parent.MaxLines,
            LineHeight = HasMeaningfulFloatValue(child.LineHeight) ? child.LineHeight : parent.LineHeight,
            Ellipsis = child.Ellipsis.Length > 0 ? child.Ellipsis : parent.Ellipsis,
            AutoSize = child.AutoSize || parent.AutoSize,
            MinFontSize = HasMeaningfulFloatValue(child.MinFontSize) ? child.MinFontSize : parent.MinFontSize,
            MaxFontSize = HasMeaningfulFloatValue(child.MaxFontSize) ? child.MaxFontSize : parent.MaxFontSize,
            TextEffect = child.TextEffect.Length > 0 ? child.TextEffect : parent.TextEffect,
            TextDirection = child.TextDirection.Length > 0 ? child.TextDirection : parent.TextDirection,
            ShowValue = child.ShowValue || parent.ShowValue,
            X1 = HasMeaningfulFloatValue(child.X1) ? child.X1 : parent.X1,
            Y1 = HasMeaningfulFloatValue(child.Y1) ? child.Y1 : parent.Y1,
            X2 = HasMeaningfulFloatValue(child.X2) ? child.X2 : parent.X2,
            Y2 = HasMeaningfulFloatValue(child.Y2) ? child.Y2 : parent.Y2,
            Children = child.Children.Count > 0 ? child.Children : parent.Children,
            Padding = child.Padding.Length > 0 ? child.Padding : parent.Padding,
            BadgeStyle = child.BadgeStyle.Length > 0 ? child.BadgeStyle : parent.BadgeStyle,
            WasPrice = child.WasPrice.Length > 0 ? child.WasPrice : parent.WasPrice,
            NowPrice = child.NowPrice.Length > 0 ? child.NowPrice : parent.NowPrice,
            Currency = child.Currency.Length > 0 ? child.Currency : parent.Currency,
            GradientType = child.GradientType.Length > 0 ? child.GradientType : parent.GradientType,
            GradientColors = child.GradientColors.Count > 0 ? child.GradientColors : parent.GradientColors,
            GradientAngle = HasMeaningfulFloatValue(child.GradientAngle) ? child.GradientAngle : parent.GradientAngle,
            GradientStartX = HasMeaningfulFloatValue(child.GradientStartX) ? child.GradientStartX : parent.GradientStartX,
            GradientStartY = HasMeaningfulFloatValue(child.GradientStartY) ? child.GradientStartY : parent.GradientStartY,
            GradientEndX = HasMeaningfulFloatValue(child.GradientEndX) ? child.GradientEndX : parent.GradientEndX,
            GradientEndY = HasMeaningfulFloatValue(child.GradientEndY) ? child.GradientEndY : parent.GradientEndY,
        };
    }

    private static bool HasMeaningfulFloatValue(float value) => MathF.Abs(value) > float.Epsilon;

    private static IReadOnlyList<CompiledElement> CompileElements(IEnumerable<SubmitRenderJobTemplateElementRequest> elements)
    {
        List<CompiledElement> compiled = [];

        foreach (SubmitRenderJobTemplateElementRequest element in elements)
        {
            compiled.Add(CompileElement(element));
        }

        return compiled;
    }

    private static CompiledElement CompileElement(SubmitRenderJobTemplateElementRequest element)
    {
        return new CompiledElement
        {
            NormalizedType = element.Type.Trim().ToLowerInvariant(),
            Left = element.Left,
            Top = element.Top,
            Width = element.Width,
            Height = element.Height,
            ForegroundColor = ParseColor(element.ForegroundColor, SKColors.Black),
            BackgroundColor = ParseColor(element.BackgroundColor, SKColors.Transparent),
            StrokeWidth = element.StrokeWidth,
            CornerRadius = element.CornerRadius,
            Fill = element.Fill,
            FontFamily = element.FontFamily,
            FontSize = element.FontSize,
            FontWeight = element.FontWeight,
            HorizontalAlign = element.HorizontalAlign,
            WordWrap = element.WordWrap,
            MaxLines = element.MaxLines,
            LineHeight = element.LineHeight,
            Ellipsis = element.Ellipsis,
            AutoSize = element.AutoSize,
            MinFontSize = element.MinFontSize,
            MaxFontSize = element.MaxFontSize,
            TextEffect = element.TextEffect.Trim().ToLowerInvariant(),
            TextDirection = element.TextDirection.Trim().ToLowerInvariant(),
            GradientType = element.GradientType.Trim().ToLowerInvariant(),
            GradientColors = element.GradientColors.Select(gradientColor => ParseColor(gradientColor, SKColors.Transparent)).ToList(),
            GradientAngle = element.GradientAngle,
            GradientStartX = element.GradientStartX,
            GradientStartY = element.GradientStartY,
            GradientEndX = element.GradientEndX,
            GradientEndY = element.GradientEndY,
            ElementId = element.ElementId,
            ShowValue = element.ShowValue,
            X1 = element.X1,
            Y1 = element.Y1,
            X2 = element.X2,
            Y2 = element.Y2,
            Children = CompileElements(element.Children),
            Padding = element.Padding,
            BadgeStyle = element.BadgeStyle.Trim().ToLowerInvariant(),
            WasPrice = element.WasPrice,
            NowPrice = element.NowPrice,
            Currency = element.Currency,
            Value = element.Value,
            Binding = element.Binding,
            Format = element.Format,
        };
    }

    private static SKColor ParseColor(string color, SKColor fallback)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return fallback;
        }

        string normalized = color.Trim();

        if (normalized.Length == 7
            && normalized[0] == '#'
            && byte.TryParse(normalized.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte red)
            && byte.TryParse(normalized.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte green)
            && byte.TryParse(normalized.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte blue))
        {
            return new SKColor(red, green, blue, 255);
        }

        if (normalized.Length == 9
            && normalized[0] == '#'
            && byte.TryParse(normalized.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte alpha)
            && byte.TryParse(normalized.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte redChannel)
            && byte.TryParse(normalized.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte greenChannel)
            && byte.TryParse(normalized.AsSpan(7, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte blueChannel))
        {
            return new SKColor(redChannel, greenChannel, blueChannel, alpha);
        }

        return fallback;
    }
}
