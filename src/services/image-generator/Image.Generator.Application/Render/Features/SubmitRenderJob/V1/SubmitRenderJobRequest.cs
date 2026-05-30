// <copyright file="SubmitRenderJobRequest.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Image.Generator.Application.Render.Features.SubmitRenderJob.V1;

/// <summary>
/// Represents a request to submit a render job.
/// </summary>
public sealed record SubmitRenderJobRequest
{
    /// <summary>
    /// Gets the display identifier.
    /// </summary>
    public Guid DisplayId { get; init; }

    /// <summary>
    /// Gets the requested output type.
    /// </summary>
    public string OutputType { get; init; } = "png";

    /// <summary>
    /// Gets the palette colors used for rendering.
    /// </summary>
    public IList<string> PaletteColors { get; init; } = [];

    /// <summary>
    /// Gets the render template payload.
    /// </summary>
    public SubmitRenderJobTemplateRequest Template { get; init; } = new();

    /// <summary>
    /// Gets the data bindings used by template elements.
    /// </summary>
    public IDictionary<string, string> Data { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the optional provider profile that constrains output to a specific ESL vendor's screen specifications.
    /// </summary>
    public ProviderProfile? ProviderProfile { get; init; }
}

/// <summary>
/// Represents template metadata and elements for a render job.
/// </summary>
public sealed record SubmitRenderJobTemplateRequest
{
    /// <summary>
    /// Gets the template width in pixels.
    /// </summary>
    public int Width { get; init; } = 1200;

    /// <summary>
    /// Gets the template height in pixels.
    /// </summary>
    public int Height { get; init; } = 825;

    /// <summary>
    /// Gets the template background color.
    /// </summary>
    public string BackgroundColor { get; init; } = "#FFFFFF";

    /// <summary>
    /// Gets a value indicating whether Left/Top/Width/Height on all elements are treated as percentages (0-100).
    /// </summary>
    public bool UsePercentagePositioning { get; init; }

    /// <summary>
    /// Gets the template elements.
    /// </summary>
    public IList<SubmitRenderJobTemplateElementRequest> Elements { get; init; } = [];

    // --- Template inheritance ---

    /// <summary>
    /// Gets the optional unique identifier for this template. Used when other templates reference this as a parent.
    /// </summary>
    public string TemplateId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the parent template identifier. When set, elements are inherited from the referenced parent template. Elements in this template
    /// with matching ElementId override parent elements. New elements are appended.
    /// </summary>
    public string ParentTemplateId { get; init; } = string.Empty;
}

/// <summary>
/// Represents a single template element in a render job template.
/// </summary>
public sealed record SubmitRenderJobTemplateElementRequest
{
    /// <summary>
    /// Gets the optional unique identifier for this element. Required for template inheritance overrides.
    /// </summary>
    public string ElementId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the element type.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Gets the left position.
    /// </summary>
    public float Left { get; init; }

    /// <summary>
    /// Gets the top position.
    /// </summary>
    public float Top { get; init; }

    /// <summary>
    /// Gets the element width.
    /// </summary>
    public float Width { get; init; }

    /// <summary>
    /// Gets the element height.
    /// </summary>
    public float Height { get; init; }

    /// <summary>
    /// Gets the literal element value.
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// Gets the binding key.
    /// </summary>
    public string Binding { get; init; } = string.Empty;

    /// <summary>
    /// Gets the output format string.
    /// </summary>
    public string Format { get; init; } = string.Empty;

    /// <summary>
    /// Gets the font family.
    /// </summary>
    public string FontFamily { get; init; } = string.Empty;

    /// <summary>
    /// Gets the font size.
    /// </summary>
    public float FontSize { get; init; } = 16;

    /// <summary>
    /// Gets the font weight.
    /// </summary>
    public string FontWeight { get; init; } = string.Empty;

    /// <summary>
    /// Gets the horizontal alignment.
    /// </summary>
    public string HorizontalAlign { get; init; } = "left";

    /// <summary>
    /// Gets the foreground color.
    /// </summary>
    public string ForegroundColor { get; init; } = "#000000";

    /// <summary>
    /// Gets the background color.
    /// </summary>
    public string BackgroundColor { get; init; } = string.Empty;

    /// <summary>
    /// Gets the stroke width.
    /// </summary>
    public float StrokeWidth { get; init; }

    /// <summary>
    /// Gets the corner radius.
    /// </summary>
    public float CornerRadius { get; init; }

    /// <summary>
    /// Gets a value indicating whether the shape is filled.
    /// </summary>
    public bool Fill { get; init; }

    // --- Typography expansions ---

    /// <summary>
    /// Gets a value indicating whether text wraps to fit within the element width.
    /// </summary>
    public bool WordWrap { get; init; }

    /// <summary>
    /// Gets the maximum number of lines when word wrapping. Zero means unlimited.
    /// </summary>
    public int MaxLines { get; init; }

    /// <summary>
    /// Gets the line-height multiplier for wrapped text. Default 1.2.
    /// </summary>
    public float LineHeight { get; init; } = 1.2f;

    /// <summary>
    /// Gets the overflow indicator when text exceeds MaxLines. Default "…".
    /// </summary>
    public string Ellipsis { get; init; } = "…";

    /// <summary>
    /// Gets a value indicating whether font size is automatically adjusted to fit the element bounds.
    /// </summary>
    public bool AutoSize { get; init; }

    /// <summary>
    /// Gets the minimum font size when AutoSize is enabled. Default 8.
    /// </summary>
    public float MinFontSize { get; init; } = 8;

    /// <summary>
    /// Gets the maximum font size when AutoSize is enabled. Default 72.
    /// </summary>
    public float MaxFontSize { get; init; } = 72;

    /// <summary>
    /// Gets the text effect: "none", "outline", "shadow", "strikethrough".
    /// </summary>
    public string TextEffect { get; init; } = "none";

    // --- Barcode expansions ---

    /// <summary>
    /// Gets a value indicating whether the barcode value is rendered as human-readable text below the barcode.
    /// </summary>
    public bool ShowValue { get; init; }

    // --- Line element ---

    /// <summary>
    /// Gets the start X coordinate for line elements.
    /// </summary>
    public float X1 { get; init; }

    /// <summary>
    /// Gets the start Y coordinate for line elements.
    /// </summary>
    public float Y1 { get; init; }

    /// <summary>
    /// Gets the end X coordinate for line elements.
    /// </summary>
    public float X2 { get; init; }

    /// <summary>
    /// Gets the end Y coordinate for line elements.
    /// </summary>
    public float Y2 { get; init; }

    // --- Container / group element ---

    /// <summary>
    /// Gets the child elements for container-type elements.
    /// </summary>
    public IList<SubmitRenderJobTemplateElementRequest> Children { get; init; } = [];

    /// <summary>
    /// Gets the padding inside a container element: "top,right,bottom,left" or single value for all sides.
    /// </summary>
    public string Padding { get; init; } = string.Empty;

    // --- Badge element ---

    /// <summary>
    /// Gets the badge style for badge-type elements.
    /// Values: "ribbon-top-right", "ribbon-top-left", "pill", "circle", "burst".
    /// </summary>
    public string BadgeStyle { get; init; } = string.Empty;

    // --- Price element ---

    /// <summary>
    /// Gets the original price for price comparison elements (strikethrough display).
    /// </summary>
    public string WasPrice { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current or sale price for price comparison elements.
    /// </summary>
    public string NowPrice { get; init; } = string.Empty;

    /// <summary>
    /// Gets the currency symbol for price elements (e.g., "€", "$", "£").
    /// </summary>
    public string Currency { get; init; } = string.Empty;

    // --- RTL / BiDi support ---

    /// <summary>
    /// Gets the text direction for text elements. Values: "ltr", "rtl", "auto".
    /// "auto" detects direction from content using Unicode character ranges.
    /// </summary>
    public string TextDirection { get; init; } = "auto";

    // --- Gradient support ---

    /// <summary>
    /// Gets the gradient type for fill. Values: "none", "linear", "radial".
    /// </summary>
    public string GradientType { get; init; } = "none";

    /// <summary>
    /// Gets the gradient color stops as hex colors. At least two required when gradient is enabled.
    /// </summary>
    public IList<string> GradientColors { get; init; } = [];

    /// <summary>
    /// Gets the gradient angle in degrees for linear gradients. 0 = left-to-right, 90 = top-to-bottom.
    /// </summary>
    public float GradientAngle { get; init; }

    /// <summary>
    /// Gets the normalized start X (0-1) for linear gradient. Overrides GradientAngle when both are set.
    /// </summary>
    public float GradientStartX { get; init; }

    /// <summary>
    /// Gets the normalized start Y (0-1) for linear gradient.
    /// </summary>
    public float GradientStartY { get; init; }

    /// <summary>
    /// Gets the normalized end X (0-1) for linear gradient.
    /// </summary>
    public float GradientEndX { get; init; }

    /// <summary>
    /// Gets the normalized end Y (0-1) for linear gradient.
    /// </summary>
    public float GradientEndY { get; init; }

    // --- Template inheritance ---

    /// <summary>
    /// Gets the optional unique identifier for this template. Used when other templates reference this as a parent.
    /// </summary>
    public string TemplateId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the parent template identifier. When set, elements are inherited from the referenced parent template. Elements in this template
    /// with matching TemplateId override parent elements. New elements are appended.
    /// </summary>
    public string ParentTemplateId { get; init; } = string.Empty;
}

/// <summary>
/// Describes the screen capabilities of a specific ESL vendor and model.
/// When provided, the renderer applies vendor-specific constraints (resolution, colors, format).
/// </summary>
public sealed record ProviderProfile
{
    /// <summary>
    /// Gets the vendor identifier, e.g. "hanshow", "pricer", "ses", "solum".
    /// </summary>
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the exact screen width in pixels.
    /// </summary>
    public int ScreenWidth { get; init; }

    /// <summary>
    /// Gets the exact screen height in pixels.
    /// </summary>
    public int ScreenHeight { get; init; }

    /// <summary>
    /// Gets the supported display colors. Common values: ["black","white"], ["black","white","red"], ["black","white","red","yellow"].
    /// </summary>
    public IList<string> SupportedColors { get; init; } = [];

    /// <summary>
    /// Gets the bits per pixel (color depth). 1 = B/W, 2 = B/W/R or B/W/Y, 4 = grayscale.
    /// </summary>
    public int ColorDepth { get; init; } = 1;

    /// <summary>
    /// Gets the preferred output format for this vendor.
    /// </summary>
    public string PreferredFormat { get; init; } = "png";

    /// <summary>
    /// Gets the dithering level: 0 = none, 1 = Floyd-Steinberg, 2 = ordered/Bayer.
    /// </summary>
    public int DitherLevel { get; init; } = 1;

    /// <summary>
    /// Gets a value indicating whether the renderer quantizes output to exactly the supported colors.
    /// </summary>
    public bool QuantizeToPalette { get; init; } = true;
}
