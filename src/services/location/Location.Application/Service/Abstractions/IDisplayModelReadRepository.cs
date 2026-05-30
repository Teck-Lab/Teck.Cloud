// <copyright file="IDisplayModelReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Abstractions;

public interface IDisplayModelReadRepository
{
    ValueTask<IReadOnlyList<DisplayModelSnapshot>> ListAsync(string tenantId, CancellationToken cancellationToken);
}

public sealed record DisplayModelSnapshot(
    string DisplayModelId,
    string Name,
    int Width,
    int Height);