// <copyright file="ILocationGroupReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Abstractions;

/// <summary>
/// Repository for read operations on location groups.
/// </summary>
public interface ILocationGroupReadRepository
{
    /// <summary>
    /// Gets a location group snapshot by identifier.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="locationGroupId">The location group identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The location group snapshot when found; otherwise <see langword="null"/>.</returns>
    ValueTask<LocationGroupSnapshot?> GetByIdAsync(string tenantId, string locationGroupId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists location groups for a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of location group snapshots.</returns>
    ValueTask<IReadOnlyList<LocationGroupSnapshot>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken);
}

/// <summary>
/// Snapshot model for a location group.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="LocationGroupId">The location group identifier.</param>
/// <param name="Name">The location group name.</param>
public sealed record LocationGroupSnapshot(
    string TenantId,
    string LocationGroupId,
    string Name);
