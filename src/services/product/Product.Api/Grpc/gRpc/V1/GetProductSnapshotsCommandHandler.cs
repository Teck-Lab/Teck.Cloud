// <copyright file="GetProductSnapshotsCommandHandler.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Grpc.Contracts.Remote.V1.Products;

namespace Product.Api.Grpc.V1;

/// <summary>
/// Handles internal product snapshot RPC requests.
/// </summary>
internal sealed class GetProductSnapshotsCommandHandler
    : FastEndpoints.ICommandHandler<GetProductSnapshotsCommand, GetProductSnapshotsRpcResult>
{
    private static readonly IReadOnlyDictionary<Guid, ProductSnapshotRpcItem> SnapshotsByProductId =
        new Dictionary<Guid, ProductSnapshotRpcItem>
        {
            [Guid.Parse("11111111-1111-1111-1111-111111111111")] = new ProductSnapshotRpcItem
            {
                ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Sparkling Water 500ml",
                Sku = "WTR-500",
                Barcode = "1234567890123",
                SnapshotVersion = "v1",
            },
            [Guid.Parse("22222222-2222-2222-2222-222222222222")] = new ProductSnapshotRpcItem
            {
                ProductId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Roasted Almonds 200g",
                Sku = "ALM-200",
                Barcode = "2234567890123",
                SnapshotVersion = "v3",
            },
            [Guid.Parse("33333333-3333-3333-3333-333333333333")] = new ProductSnapshotRpcItem
            {
                ProductId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "Protein Bar Chocolate",
                Sku = "PRB-CHO",
                Barcode = "3234567890123",
                SnapshotVersion = "v2",
            },
        };

    /// <inheritdoc/>
    public Task<GetProductSnapshotsRpcResult> ExecuteAsync(GetProductSnapshotsCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ct.ThrowIfCancellationRequested();

        GetProductSnapshotsRpcResult result = new();

        foreach (Guid productId in command.ProductIds.Distinct())
        {
            if (SnapshotsByProductId.TryGetValue(productId, out ProductSnapshotRpcItem? snapshot))
            {
                result.Items.Add(snapshot);
            }
        }

        return Task.FromResult(result);
    }
}
