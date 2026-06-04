// <copyright file="EnqueueRenderJobZoneRpcItem.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace SharedKernel.Grpc.Contracts.Remote.V1.Labels;

/// <summary>
/// Zone assignment used by legacy render job callers.
/// </summary>
public sealed class EnqueueRenderJobZoneRpcItem
{
    /// <summary>
    /// Gets or sets the zone index.
    /// </summary>
    public int ZoneIndex { get; set; }

    /// <summary>
    /// Gets or sets the product identifier assigned to the zone.
    /// </summary>
    public Guid ProductId { get; set; }
}
