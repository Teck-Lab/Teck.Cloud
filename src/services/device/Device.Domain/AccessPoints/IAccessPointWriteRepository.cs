// <copyright file="IAccessPointWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Domain.AccessPoints;

/// <summary>
/// Write repository for <see cref="AccessPoint"/> entities.
/// </summary>
public interface IAccessPointWriteRepository
{
    /// <summary>
    /// Adds a new access point.
    /// </summary>
    /// <param name="accessPoint">The access point to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(AccessPoint accessPoint, CancellationToken ct);

    /// <summary>
    /// Updates an access point.
    /// </summary>
    /// <param name="accessPoint">The access point to update.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(AccessPoint accessPoint, CancellationToken ct);
}
