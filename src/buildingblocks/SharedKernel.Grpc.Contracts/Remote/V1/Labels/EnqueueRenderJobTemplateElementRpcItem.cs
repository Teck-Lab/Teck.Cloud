// <copyright file="EnqueueRenderJobTemplateElementRpcItem.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace SharedKernel.Grpc.Contracts.Remote.V1.Labels;

public sealed class EnqueueRenderJobTemplateElementRpcItem
{
    public string Type { get; set; } = string.Empty;

    public float Left { get; set; }

    public float Top { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }

    public string Value { get; set; } = string.Empty;

    public string Binding { get; set; } = string.Empty;

    public string Format { get; set; } = string.Empty;

    public string FontFamily { get; set; } = string.Empty;

    public float FontSize { get; set; } = 16;

    public string FontWeight { get; set; } = string.Empty;

    public string HorizontalAlign { get; set; } = "left";

    public string ForegroundColor { get; set; } = "#000000";

    public string BackgroundColor { get; set; } = string.Empty;

    public float StrokeWidth { get; set; }

    public float CornerRadius { get; set; }

    public bool Fill { get; set; }
}
