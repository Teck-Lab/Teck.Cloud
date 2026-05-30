// <copyright file="ITemplateScopeSettingsReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Abstractions;

public interface ITemplateScopeSettingsReadRepository
{
    ValueTask<TemplateScopeSettingsSnapshot?> GetByScopeAsync(
        string tenantId,
        string scopeType,
        string scopeKey,
        CancellationToken cancellationToken);
}

public sealed record TemplateScopeSettingsSnapshot(
    string TenantId,
    string ScopeType,
    string ScopeKey,
    string SettingsJson);
