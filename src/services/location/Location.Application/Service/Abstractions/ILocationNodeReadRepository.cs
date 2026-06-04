// <copyright file="ILocationNodeReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Abstractions;

/// <summary>
/// Repository for read operations on location nodes.
/// </summary>
public interface ILocationNodeReadRepository
{
    /// <summary>
    /// Gets a location node snapshot by node identifier.
    /// </summary>
    /// <param name="locationNodeId">The location node identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The location node snapshot when found; otherwise <see langword="null"/>.</returns>
    ValueTask<LocationNodeSnapshot?> GetByIdAsync(string locationNodeId, CancellationToken cancellationToken);

    /// <summary>
    /// Searches location nodes by optional name query.
    /// </summary>
    /// <param name="query">Optional name query. When <see langword="null"/>, all nodes are returned.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of matching location node snapshots.</returns>
    ValueTask<IReadOnlyList<LocationNodeSnapshot>> SearchByNameAsync(string? query, CancellationToken cancellationToken);
}

/// <summary>
/// Snapshot model for a location node.
/// </summary>
/// <param name="LocationNodeId">The location node identifier.</param>
/// <param name="ParentLocationNodeId">The parent node identifier.</param>
/// <param name="TemplateId">The template identifier assigned to the node.</param>
/// <param name="Name">The location node name.</param>
/// <param name="LocationGroupId">The location group identifier.</param>
/// <param name="Aisle">The aisle value.</param>
/// <param name="Shelf">The shelf value.</param>
public sealed record LocationNodeSnapshot(
    string LocationNodeId,
    string? ParentLocationNodeId,
    string? TemplateId,
    string? Name,
    string? LocationGroupId,
    string? Aisle,
    string? Shelf);
