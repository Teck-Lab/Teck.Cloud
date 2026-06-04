// <copyright file="ITemplateScopeSettingsReadRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Abstractions;

/// <summary>
/// Repository for read operations on template scope settings.
/// </summary>
public interface ITemplateScopeSettingsReadRepository
{
    /// <summary>
    /// Gets template scope settings for a scope.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="scopeType">The scope type.</param>
    /// <param name="scopeKey">The scope key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The scope settings snapshot when found; otherwise <see langword="null"/>.</returns>
    ValueTask<TemplateScopeSettingsSnapshot?> GetByScopeAsync(
        string tenantId,
        string scopeType,
        string scopeKey,
        CancellationToken cancellationToken);
}

/// <summary>
/// Snapshot model for scope settings.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="ScopeType">The scope type.</param>
/// <param name="ScopeKey">The scope key.</param>
/// <param name="SettingsJson">The serialized settings payload.</param>
public sealed record TemplateScopeSettingsSnapshot(
    string TenantId,
    string ScopeType,
    string ScopeKey,
    string SettingsJson);
