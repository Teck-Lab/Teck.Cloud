// <copyright file="GetProductSnapshotsCommand.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FastEndpoints;

namespace SharedKernel.Grpc.Contracts.Remote.V1.Products;

/// <summary>
/// Command to request product snapshots from the Product service.
/// </summary>
public sealed class GetProductSnapshotsCommand : ICommand<GetProductSnapshotsRpcResult>
{
    /// <summary>
    /// Gets or sets the downstream service name making the request.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets the product identifiers to retrieve snapshots for.
    /// </summary>
    public IList<Guid> ProductIds { get; init; } = [];
}
