// <copyright file="ProductSnapshotRpcItem.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace SharedKernel.Grpc.Contracts.Remote.V1.Products;

public sealed class ProductSnapshotRpcItem
{
    public Guid ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Sku { get; set; }

    public string? Barcode { get; set; }

    public string SnapshotVersion { get; set; } = string.Empty;
}
