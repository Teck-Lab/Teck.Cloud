// <copyright file="ILocationNodeReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Abstractions;

public interface ILocationNodeReadRepository
{
    ValueTask<LocationNodeSnapshot?> GetByIdAsync(string locationNodeId, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<LocationNodeSnapshot>> SearchByNameAsync(string? query, CancellationToken cancellationToken);
}

public sealed record LocationNodeSnapshot(
    string LocationNodeId,
    string? ParentLocationNodeId,
    string? TemplateId,
    string? Name,
    string? LocationGroupId,
    string? Aisle,
    string? Shelf);
