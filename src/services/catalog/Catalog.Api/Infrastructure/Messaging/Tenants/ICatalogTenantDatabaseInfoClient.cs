// <copyright file="ICatalogTenantDatabaseInfoClient.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Grpc.Contracts.Remote.V1.Tenants;

namespace Catalog.Api.Infrastructure.Messaging.Tenants;

/// <summary>
/// Abstraction for retrieving tenant database metadata for Catalog miss resolution.
/// </summary>
internal interface ICatalogTenantDatabaseInfoClient
{
    /// <summary>
    /// Gets tenant database metadata.
    /// </summary>
    /// <param name="tenantId">Tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tenant metadata when available; otherwise null.</returns>
    Task<TenantDatabaseInfoRpcResult?> GetTenantDatabaseInfoAsync(string tenantId, CancellationToken cancellationToken);
}
