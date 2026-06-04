// <copyright file="ITemplateScopeSettingsWriteRepository.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Location.Application.Service.Abstractions;

/// <summary>
/// Repository for write operations on template scope settings.
/// </summary>
public interface ITemplateScopeSettingsWriteRepository
{
    /// <summary>
    /// Creates or updates template scope settings.
    /// </summary>
    /// <param name="snapshot">The scope settings snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask UpsertAsync(TemplateScopeSettingsSnapshot snapshot, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes template scope settings for a scope.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="scopeType">The scope type.</param>
    /// <param name="scopeKey">The scope key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask DeleteAsync(string tenantId, string scopeType, string scopeKey, CancellationToken cancellationToken);
}
