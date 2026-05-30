// <copyright file="EnqueueRenderJobCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FastEndpoints;

namespace SharedKernel.Grpc.Contracts.Remote.V1.Labels;

public sealed class EnqueueRenderJobCommand : ICommand<EnqueueRenderJobRpcResult>
{
    public Guid JobId { get; set; }

    public Guid DisplayId { get; set; }

    public string OutputType { get; set; } = "png";

    public IList<string> PaletteColors { get; init; } = [];

    public EnqueueRenderJobTemplateRpcItem? Template { get; set; }

    public IDictionary<string, string> Data { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // Legacy fallback fields for older callers.
    public string LayoutName { get; set; } = string.Empty;

    public IList<EnqueueRenderJobZoneRpcItem> Zones { get; init; } = [];
}
