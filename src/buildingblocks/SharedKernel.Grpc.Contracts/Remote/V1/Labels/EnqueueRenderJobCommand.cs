// <copyright file="EnqueueRenderJobCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FastEndpoints;

namespace SharedKernel.Grpc.Contracts.Remote.V1.Labels;

/// <summary>
/// Command to enqueue a label render job in the Labels service.
/// </summary>
public sealed class EnqueueRenderJobCommand : ICommand<EnqueueRenderJobRpcResult>
{
    /// <summary>
    /// Gets or sets the render job identifier.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Gets or sets the display identifier used for rendering.
    /// </summary>
    public Guid DisplayId { get; set; }

    /// <summary>
    /// Gets or sets the output image format.
    /// </summary>
    public string OutputType { get; set; } = "png";

    /// <summary>
    /// Gets the palette colors used to render the label.
    /// </summary>
    public IList<string> PaletteColors { get; init; } = [];

    /// <summary>
    /// Gets or sets the template used to render the job.
    /// </summary>
    public EnqueueRenderJobTemplateRpcItem? Template { get; set; }

    /// <summary>
    /// Gets key-value data bound to template fields.
    /// </summary>
    public IDictionary<string, string> Data { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // Legacy fallback fields for older callers.

    /// <summary>
    /// Gets or sets the legacy layout name used by older callers.
    /// </summary>
    public string LayoutName { get; set; } = string.Empty;

    /// <summary>
    /// Gets zone assignments used by the legacy rendering flow.
    /// </summary>
    public IList<EnqueueRenderJobZoneRpcItem> Zones { get; init; } = [];
}
