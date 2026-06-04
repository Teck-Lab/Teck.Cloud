// <copyright file="EnqueueRenderJobTemplateElementRpcItem.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace SharedKernel.Grpc.Contracts.Remote.V1.Labels;

/// <summary>
/// Template element definition for a render job.
/// </summary>
public sealed class EnqueueRenderJobTemplateElementRpcItem
{
    /// <summary>
    /// Gets or sets the element type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the left coordinate.
    /// </summary>
    public float Left { get; set; }

    /// <summary>
    /// Gets or sets the top coordinate.
    /// </summary>
    public float Top { get; set; }

    /// <summary>
    /// Gets or sets the element width.
    /// </summary>
    public float Width { get; set; }

    /// <summary>
    /// Gets or sets the element height.
    /// </summary>
    public float Height { get; set; }

    /// <summary>
    /// Gets or sets the static element value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the data binding key.
    /// </summary>
    public string Binding { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display format string.
    /// </summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the font family.
    /// </summary>
    public string FontFamily { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the font size.
    /// </summary>
    public float FontSize { get; set; } = 16;

    /// <summary>
    /// Gets or sets the font weight.
    /// </summary>
    public string FontWeight { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the horizontal text alignment.
    /// </summary>
    public string HorizontalAlign { get; set; } = "left";

    /// <summary>
    /// Gets or sets the foreground color.
    /// </summary>
    public string ForegroundColor { get; set; } = "#000000";

    /// <summary>
    /// Gets or sets the background color.
    /// </summary>
    public string BackgroundColor { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stroke width.
    /// </summary>
    public float StrokeWidth { get; set; }

    /// <summary>
    /// Gets or sets the corner radius.
    /// </summary>
    public float CornerRadius { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the element is filled.
    /// </summary>
    public bool Fill { get; set; }
}
