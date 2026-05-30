// <copyright file="ILocationGroupReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Abstractions;

public interface ILocationGroupReadRepository
{
    ValueTask<LocationGroupSnapshot?> GetByIdAsync(string tenantId, string locationGroupId, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<LocationGroupSnapshot>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken);
}

public sealed record LocationGroupSnapshot(
    string TenantId,
    string LocationGroupId,
    string Name);
