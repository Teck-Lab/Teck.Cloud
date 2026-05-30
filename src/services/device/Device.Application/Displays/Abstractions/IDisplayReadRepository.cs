// <copyright file="IDisplayReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.Displays.Abstractions;

/// <summary>
/// Snapshot returned per display in a list query.
/// </summary>
/// <param name="DisplayId">Primary key.</param>
/// <param name="ShortSerial">4-byte serial in XX-XX-XX-XX format.</param>
/// <param name="LongSerial">Decimal serial (null until first heartbeat).</param>
/// <param name="LocationNodeId">Location the display is assigned to.</param>
/// <param name="DeviceDefinitionId">Optional device model identifier.</param>
/// <param name="CreatedAt">Registration timestamp (UTC).</param>
public sealed record DisplaySnapshot(
    Guid DisplayId,
    string ShortSerial,
    long? LongSerial,
    string LocationNodeId,
    Guid? DeviceDefinitionId,
    DateTimeOffset CreatedAt);

/// <summary>
/// Read repository for display queries.
/// </summary>
public interface IDisplayReadRepository
{
    /// <summary>
    /// Returns all displays for the current tenant scoped to a location node.
    /// </summary>
    /// <param name="locationNodeId">Location node identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ordered list of display snapshots.</returns>
    ValueTask<IReadOnlyList<DisplaySnapshot>> GetByLocationAsync(
        string locationNodeId,
        CancellationToken cancellationToken);
}
