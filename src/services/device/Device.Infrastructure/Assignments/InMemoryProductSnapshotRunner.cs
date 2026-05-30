// <copyright file="InMemoryProductSnapshotRunner.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Application.Assignments.Abstractions;
using FastEndpoints;
using Grpc.Core;
using SharedKernel.Grpc.Contracts.Remote.V1.Products;

namespace Device.Infrastructure.Assignments;

internal sealed class InMemoryProductSnapshotRunner : IProductSnapshotRunner
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

    public async ValueTask<IReadOnlyList<ProductSnapshotItem>> GetSnapshotsAsync(
        string serviceName,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        GetProductSnapshotsCommand command = new()
        {
            ServiceName = serviceName,
        };

        foreach (Guid productId in productIds)
        {
            command.ProductIds.Add(productId);
        }

        GetProductSnapshotsRpcResult rpcResult;
        try
        {
            rpcResult = await command
                .RemoteExecuteAsync(new CallOptions(cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (rpcResult is not null)
            {
                return rpcResult.Items
                    .Select(item => new ProductSnapshotItem(
                        ProductId: item.ProductId,
                        Name: item.Name,
                        Sku: item.Sku,
                        Barcode: item.Barcode,
                        SnapshotVersion: item.SnapshotVersion))
                    .ToArray();
            }
        }
        catch (RpcException)
        {
            // Fall through to local data when transport is unavailable.
        }
        catch (InvalidOperationException)
        {
            // Fall through when FE remote registration is not configured.
        }

        rpcResult = new GetProductSnapshotsRpcResult();
        foreach (Guid productId in command.ProductIds)
        {
            if (SnapshotsByProductId.TryGetValue(productId, out ProductSnapshotRpcItem? snapshot))
            {
                rpcResult.Items.Add(snapshot);
            }
        }

        IReadOnlyList<ProductSnapshotItem> result = rpcResult.Items
            .Select(item => new ProductSnapshotItem(
                ProductId: item.ProductId,
                Name: item.Name,
                Sku: item.Sku,
                Barcode: item.Barcode,
                SnapshotVersion: item.SnapshotVersion))
            .ToArray();

        return result;
    }
}
