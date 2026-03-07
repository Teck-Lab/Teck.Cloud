// <copyright file="ITenantDatabaseStrategyResolver.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace Web.Edge.Services;

/// <summary>
/// Resolves tenant database strategy metadata for downstream routing decisions.
/// </summary>
public interface ITenantDatabaseStrategyResolver
{
    /// <summary>
    /// Resolves tenant database metadata for a downstream service.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="serviceName">The downstream service name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The lookup result.</returns>
    Task<TenantDatabaseStrategyLookupResult> ResolveAsync(string tenantId, string? serviceName, CancellationToken cancellationToken);
}
