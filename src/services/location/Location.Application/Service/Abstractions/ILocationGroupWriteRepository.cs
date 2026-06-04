// <copyright file="ILocationGroupWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Abstractions;

/// <summary>
/// Repository for write operations on location groups.
/// </summary>
public interface ILocationGroupWriteRepository
{
    /// <summary>
    /// Creates or updates a location group snapshot.
    /// </summary>
    /// <param name="snapshot">The location group snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask UpsertAsync(LocationGroupSnapshot snapshot, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a location group.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="locationGroupId">The location group identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask DeleteAsync(string tenantId, string locationGroupId, CancellationToken cancellationToken);
}
