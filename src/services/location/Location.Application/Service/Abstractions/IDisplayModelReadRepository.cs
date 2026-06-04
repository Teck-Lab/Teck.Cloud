// <copyright file="IDisplayModelReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Abstractions;

/// <summary>
/// Repository for read operations on display models.
/// </summary>
public interface IDisplayModelReadRepository
{
    /// <summary>
    /// Lists display models for a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of display model snapshots.</returns>
    ValueTask<IReadOnlyList<DisplayModelSnapshot>> ListAsync(string tenantId, CancellationToken cancellationToken);
}

/// <summary>
/// Snapshot model for a display model.
/// </summary>
/// <param name="DisplayModelId">The display model identifier.</param>
/// <param name="Name">The display model name.</param>
/// <param name="Width">The display width.</param>
/// <param name="Height">The display height.</param>
public sealed record DisplayModelSnapshot(
    string DisplayModelId,
    string Name,
    int Width,
    int Height);
