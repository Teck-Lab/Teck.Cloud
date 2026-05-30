// <copyright file="EnqueueRenderJobTemplateRpcItem.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace SharedKernel.Grpc.Contracts.Remote.V1.Labels;

public sealed class EnqueueRenderJobTemplateRpcItem
{
    public int Width { get; set; } = 1200;

    public int Height { get; set; } = 825;

    public string BackgroundColor { get; set; } = "#FFFFFF";

    public IList<EnqueueRenderJobTemplateElementRpcItem> Elements { get; init; } = [];
}
