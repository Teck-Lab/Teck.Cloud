// <copyright file="EnqueueRenderJobRpcResult.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace SharedKernel.Grpc.Contracts.Remote.V1.Labels;

/// <summary>
/// Result returned after a render job enqueue request.
/// </summary>
public sealed class EnqueueRenderJobRpcResult
{
    /// <summary>
    /// Gets or sets the render job identifier.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Gets or sets the enqueue status.
    /// </summary>
    public string Status { get; set; } = string.Empty;
}
