// <copyright file="IAccessPointReadModelRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Device.Domain.AccessPoints;

namespace Device.Application.AccessPoints.Repositories;

/// <summary>
/// Snapshot returned per access point in read-model queries.
/// </summary>
/// <param name="AccessPointId">Primary key.</param>
/// <param name="SerialNumber">Supplier serial number.</param>
/// <param name="Vendor">Vendor name.</param>
/// <param name="LocationNodeId">Assigned location node.</param>
/// <param name="Status">Operational status.</param>
/// <param name="MaxCapacity">Maximum supported display count.</param>
/// <param name="CurrentLoad">Current assigned display count.</param>
public sealed record AccessPointSnapshot(
    Guid AccessPointId,
    string SerialNumber,
    string Vendor,
    string LocationNodeId,
    AccessPointStatus Status,
    int MaxCapacity,
    int CurrentLoad);

/// <summary>
/// Read-model repository for access point projections.
/// </summary>
public interface IAccessPointReadModelRepository
{
    /// <summary>
    /// Returns all access point snapshots for a location node.
    /// </summary>
    /// <param name="locationNodeId">Location node identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Access point snapshots for the location.</returns>
    ValueTask<IReadOnlyList<AccessPointSnapshot>> GetByLocationAsync(
        string locationNodeId,
        CancellationToken cancellationToken);
}
