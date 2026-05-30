// <copyright file="ILocationGroupWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Abstractions;

public interface ILocationGroupWriteRepository
{
    ValueTask UpsertAsync(LocationGroupSnapshot snapshot, CancellationToken cancellationToken);

    ValueTask DeleteAsync(string tenantId, string locationGroupId, CancellationToken cancellationToken);
}
