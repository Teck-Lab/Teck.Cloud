// <copyright file="ILocationNodeResolver.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Device.Application.AccessPoints;

public interface ILocationNodeResolver
{
    ValueTask<IReadOnlyList<string>> GetAncestorChainAsync(string locationNodeId, CancellationToken cancellationToken);
}
