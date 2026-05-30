// <copyright file="ITemplateScopeSettingsWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Abstractions;

public interface ITemplateScopeSettingsWriteRepository
{
    ValueTask UpsertAsync(TemplateScopeSettingsSnapshot snapshot, CancellationToken cancellationToken);

    ValueTask DeleteAsync(string tenantId, string scopeType, string scopeKey, CancellationToken cancellationToken);
}
