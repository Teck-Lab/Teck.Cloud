// <copyright file="ILocationNodeResolver.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.AccessPoints;

/// <summary>
/// Resolves location node ancestry chains for access point selection.
/// </summary>
public interface ILocationNodeResolver
{
    /// <summary>
    /// Gets ancestor location node identifiers for the specified node.
    /// </summary>
    /// <param name="locationNodeId">The location node identifier to resolve ancestors for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of ancestor location node identifiers ordered from nearest ancestor to root.</returns>
    ValueTask<IReadOnlyList<string>> GetAncestorChainAsync(string locationNodeId, CancellationToken cancellationToken);
}
