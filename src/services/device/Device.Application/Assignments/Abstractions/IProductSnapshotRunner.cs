// <copyright file="IProductSnapshotRunner.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.Assignments.Abstractions;

/// <summary>
/// Retrieves product snapshots used by assignment flows.
/// </summary>
public interface IProductSnapshotRunner
{
    /// <summary>
    /// Retrieves product snapshots for the requested product identifiers.
    /// </summary>
    /// <param name="serviceName">The upstream service name providing product data.</param>
    /// <param name="productIds">The product identifiers to resolve snapshots for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of product snapshots.</returns>
    ValueTask<IReadOnlyList<ProductSnapshotItem>> GetSnapshotsAsync(
        string serviceName,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken);
}

/// <summary>
/// Snapshot of product data captured for assignment rendering.
/// </summary>
/// <param name="ProductId">The product identifier.</param>
/// <param name="Name">The product name.</param>
/// <param name="Sku">The product stock keeping unit.</param>
/// <param name="Barcode">The product barcode.</param>
/// <param name="SnapshotVersion">The snapshot version marker.</param>
public sealed record ProductSnapshotItem(
    Guid ProductId,
    string Name,
    string? Sku,
    string? Barcode,
    string SnapshotVersion);
