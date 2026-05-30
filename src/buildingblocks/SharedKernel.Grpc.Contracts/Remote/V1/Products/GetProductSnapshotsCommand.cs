// <copyright file="GetProductSnapshotsCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FastEndpoints;

namespace SharedKernel.Grpc.Contracts.Remote.V1.Products;

public sealed class GetProductSnapshotsCommand : ICommand<GetProductSnapshotsRpcResult>
{
    public string ServiceName { get; set; } = string.Empty;

    public IList<Guid> ProductIds { get; init; } = [];
}
