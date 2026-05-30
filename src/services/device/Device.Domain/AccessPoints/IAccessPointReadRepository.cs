// <copyright file="IAccessPointReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Domain.AccessPoints;

/// <summary>
/// Read repository for access point queries.
/// </summary>
public interface IAccessPointReadRepository
{
    /// <summary>
    /// Gets access points assigned to a location node.
    /// </summary>
    /// <param name="locationNodeId">Location node identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Access points assigned to the location.</returns>
    Task<IReadOnlyList<AccessPoint>> GetByLocationAsync(string locationNodeId, CancellationToken ct);

    /// <summary>
    /// Gets an access point by serial number.
    /// </summary>
    /// <param name="serialNumber">Supplier serial number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching access point, if found.</returns>
    Task<AccessPoint?> GetBySerialAsync(string serialNumber, CancellationToken ct);

    /// <summary>
    /// Finds an access point for a vendor at a location node.
    /// </summary>
    /// <param name="vendor">Vendor name.</param>
    /// <param name="locationNodeId">Location node identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching access point, if found.</returns>
    Task<AccessPoint?> FindByVendorAndLocationAsync(string vendor, string locationNodeId, CancellationToken ct);
}
