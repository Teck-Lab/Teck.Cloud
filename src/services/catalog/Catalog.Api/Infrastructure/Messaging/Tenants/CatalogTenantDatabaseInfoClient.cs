// <copyright file="CatalogTenantDatabaseInfoClient.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FastEndpoints;
using Grpc.Core;
using SharedKernel.Grpc.Contracts.Remote.V1.Tenants;

namespace Catalog.Api.Infrastructure.Messaging.Tenants;

/// <summary>
/// Remote tenant metadata client backed by FastEndpoints remote command execution.
/// </summary>
internal sealed class CatalogTenantDatabaseInfoClient : ICatalogTenantDatabaseInfoClient
{
    /// <inheritdoc/>
    public async Task<TenantDatabaseInfoRpcResult?> GetTenantDatabaseInfoAsync(string tenantId, CancellationToken cancellationToken)
    {
        return await new GetTenantDatabaseInfoCommand
        {
            TenantId = tenantId,
            ServiceName = "catalog",
        }.RemoteExecuteAsync(new CallOptions(cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
