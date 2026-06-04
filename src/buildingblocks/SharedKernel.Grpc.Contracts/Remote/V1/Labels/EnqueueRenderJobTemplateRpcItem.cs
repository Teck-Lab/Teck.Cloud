// <copyright file="EnqueueRenderJobTemplateRpcItem.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace SharedKernel.Grpc.Contracts.Remote.V1.Labels;

/// <summary>
/// Template payload for a render job request.
/// </summary>
public sealed class EnqueueRenderJobTemplateRpcItem
{
    /// <summary>
    /// Gets or sets the template width.
    /// </summary>
    public int Width { get; set; } = 1200;

    /// <summary>
    /// Gets or sets the template height.
    /// </summary>
    public int Height { get; set; } = 825;

    /// <summary>
    /// Gets or sets the background color.
    /// </summary>
    public string BackgroundColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// Gets template elements included in the layout.
    /// </summary>
    public IList<EnqueueRenderJobTemplateElementRpcItem> Elements { get; init; } = [];
}
