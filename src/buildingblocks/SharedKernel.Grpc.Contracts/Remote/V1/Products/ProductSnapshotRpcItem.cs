// <copyright file="ProductSnapshotRpcItem.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace SharedKernel.Grpc.Contracts.Remote.V1.Products;

/// <summary>
/// Product snapshot payload returned by remote product queries.
/// </summary>
public sealed class ProductSnapshotRpcItem
{
    /// <summary>
    /// Gets or sets the product identifier.
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Gets or sets the product name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the product stock keeping unit.
    /// </summary>
    public string? Sku { get; set; }

    /// <summary>
    /// Gets or sets the product barcode.
    /// </summary>
    public string? Barcode { get; set; }

    /// <summary>
    /// Gets or sets the snapshot version identifier.
    /// </summary>
    public string SnapshotVersion { get; set; } = string.Empty;
}
