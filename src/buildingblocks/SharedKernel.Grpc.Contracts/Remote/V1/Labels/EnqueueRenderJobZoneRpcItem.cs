// <copyright file="EnqueueRenderJobZoneRpcItem.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace SharedKernel.Grpc.Contracts.Remote.V1.Labels;

public sealed class EnqueueRenderJobZoneRpcItem
{
    public int ZoneIndex { get; set; }

    public Guid ProductId { get; set; }
}
