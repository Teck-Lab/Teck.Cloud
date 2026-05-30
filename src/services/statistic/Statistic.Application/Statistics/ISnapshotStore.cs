// <copyright file="ISnapshotStore.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Statistic.Domain.Statistics;

namespace Statistic.Application.Statistics;

/// <summary>
/// Abstraction for reading and updating the current statistics snapshot.
/// </summary>
public interface ISnapshotStore
{
    /// <summary>
    /// Gets the current snapshot.
    /// </summary>
    StatSnapshot Current { get; }

    /// <summary>
    /// Replaces the current snapshot with a new one.
    /// </summary>
    /// <param name="snapshot">The new snapshot.</param>
    void Update(StatSnapshot snapshot);
}
