// <copyright file="EnqueueRenderJobRpcResult.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace SharedKernel.Grpc.Contracts.Remote.V1.Labels;

public sealed class EnqueueRenderJobRpcResult
{
    public Guid JobId { get; set; }

    public string Status { get; set; } = string.Empty;
}
