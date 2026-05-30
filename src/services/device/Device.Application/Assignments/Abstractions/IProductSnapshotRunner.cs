// <copyright file="IProductSnapshotRunner.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.Assignments.Abstractions;

public interface IProductSnapshotRunner
{
    ValueTask<IReadOnlyList<ProductSnapshotItem>> GetSnapshotsAsync(
        string serviceName,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken);
}

public sealed record ProductSnapshotItem(
    Guid ProductId,
    string Name,
    string? Sku,
    string? Barcode,
    string SnapshotVersion);
