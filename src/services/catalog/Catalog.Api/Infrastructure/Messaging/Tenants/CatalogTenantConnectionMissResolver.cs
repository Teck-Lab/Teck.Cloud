// <copyright file="CatalogTenantConnectionMissResolver.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Pricing;
using SharedKernel.Grpc.Contracts.Remote.V1.Tenants;
using SharedKernel.Persistence.Database.MultiTenant;

namespace Catalog.Api.Infrastructure.Messaging.Tenants;

/// <summary>
/// Resolves missing tenant message-store connections on demand.
/// </summary>
internal sealed class CatalogTenantConnectionMissResolver(
    ILogger<CatalogTenantConnectionMissResolver> logger,
    ICatalogTenantDatabaseInfoClient tenantDatabaseInfoClient,
    WolverineTenantConnectionSource tenantConnectionSource,
    IVaultTenantConnectionProvider vaultTenantConnectionProvider)
{
    private readonly ILogger<CatalogTenantConnectionMissResolver> logger = logger;
    private readonly ICatalogTenantDatabaseInfoClient tenantDatabaseInfoClient = tenantDatabaseInfoClient;
    private readonly WolverineTenantConnectionSource tenantConnectionSource = tenantConnectionSource;
    private readonly IVaultTenantConnectionProvider vaultTenantConnectionProvider = vaultTenantConnectionProvider;

    /// <summary>
    /// Resolves and returns a tenant message-store connection string.
    /// </summary>
    /// <param name="tenantId">Tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tenant connection string if resolved; otherwise null.</returns>
    public async Task<string?> ResolveAsync(string tenantId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(tenantId, out _))
        {
            return null;
        }

        TenantDatabaseInfoRpcResult? tenantInfo;
        try
        {
            tenantInfo = await this.tenantDatabaseInfoClient
                .GetTenantDatabaseInfoAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            this.logger.LogWarning(
                exception,
                "On-demand tenant resolution failed for TenantId={TenantId} while calling customer API.",
                tenantId);
            return null;
        }

        if (tenantInfo is null || !tenantInfo.Found)
        {
            this.logger.LogWarning(
                "On-demand tenant resolution returned no tenant metadata. TenantId={TenantId}; Detail={Detail}",
                tenantId,
                tenantInfo?.ErrorDetail ?? "No response.");
            return null;
        }

        DatabaseStrategy.TryFromName(
            tenantInfo.DatabaseStrategy,
            ignoreCase: true,
            out DatabaseStrategy? strategy);

        DatabaseStrategy effectiveStrategy = strategy ?? DatabaseStrategy.Shared;
        if (effectiveStrategy == DatabaseStrategy.Shared)
        {
            return this.tenantConnectionSource.DefaultWriteConnectionString;
        }

        string preferredLookupKey = !string.IsNullOrWhiteSpace(tenantInfo.Identifier)
            ? tenantInfo.Identifier
            : tenantId;

        try
        {
            (string writeConnectionString, _) = await this.vaultTenantConnectionProvider
                .GetAsync(preferredLookupKey, cancellationToken)
                .ConfigureAwait(false);

            return writeConnectionString;
        }
        catch (TenantConnectionNotFoundException) when (!string.Equals(preferredLookupKey, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                (string writeConnectionString, _) = await this.vaultTenantConnectionProvider
                    .GetAsync(tenantId, cancellationToken)
                    .ConfigureAwait(false);

                return writeConnectionString;
            }
            catch (Exception exception)
            {
                this.logger.LogWarning(
                    exception,
                    "On-demand tenant resolution fallback lookup failed for TenantId={TenantId}.",
                    tenantId);
                return null;
            }
        }
        catch (Exception exception)
        {
            this.logger.LogWarning(
                exception,
                "On-demand tenant resolution failed for TenantId={TenantId} during vault lookup.",
                tenantId);
            return null;
        }
    }
}
