// <copyright file="GetProductSnapshotsRpcResult.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace SharedKernel.Grpc.Contracts.Remote.V1.Products;

public sealed class GetProductSnapshotsRpcResult
{
    public IList<ProductSnapshotRpcItem> Items { get; init; } = [];
}
