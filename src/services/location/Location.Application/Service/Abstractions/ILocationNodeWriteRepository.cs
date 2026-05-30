// <copyright file="ILocationNodeWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Abstractions;

/// <summary>
/// Repository for write operations on location nodes.
/// </summary>
public interface ILocationNodeWriteRepository
{
    /// <summary>
    /// Creates a new location node.
    /// </summary>
    /// <param name="snapshot">The node snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask CreateAsync(LocationNodeSnapshot snapshot, CancellationToken cancellationToken);

    /// <summary>
    /// Checks whether a node with the given identifier exists for the tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="locationNodeId">The location node identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the node exists; otherwise false.</returns>
    ValueTask<bool> ExistsAsync(string tenantId, string locationNodeId, CancellationToken cancellationToken);

    /// <summary>
    /// Checks whether the tenant already has a node with the given name.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="name">The node name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a node with the name exists; otherwise false.</returns>
    ValueTask<bool> NameExistsAsync(string tenantId, string name, CancellationToken cancellationToken);
}
