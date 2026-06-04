// <copyright file="GetProductSnapshotsRpcResult.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace SharedKernel.Grpc.Contracts.Remote.V1.Products;

/// <summary>
/// Result containing product snapshot items.
/// </summary>
public sealed class GetProductSnapshotsRpcResult
{
    /// <summary>
    /// Gets the product snapshot items returned by the remote call.
    /// </summary>
    public IList<ProductSnapshotRpcItem> Items { get; init; } = [];
}
