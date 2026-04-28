// <copyright file="CustomerTenantConnectionMissResolver.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Tenants.ReadModels;
using Customer.Application.Tenants.Repositories;
using SharedKernel.Core.Pricing;
using SharedKernel.Persistence.Database.MultiTenant;

namespace Customer.Api.Infrastructure.Messaging.Tenants;

/// <summary>
/// Resolves missing tenant message-store connections on demand.
/// </summary>
internal sealed class CustomerTenantConnectionMissResolver(
    ILogger<CustomerTenantConnectionMissResolver> logger,
    ITenantReadRepository tenantReadRepository,
    WolverineTenantConnectionSource tenantConnectionSource,
    IVaultTenantConnectionProvider vaultTenantConnectionProvider)
{
    private readonly ILogger<CustomerTenantConnectionMissResolver> logger = logger;
    private readonly ITenantReadRepository tenantReadRepository = tenantReadRepository;
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
        if (!Guid.TryParse(tenantId, out Guid parsedTenantId))
        {
            return null;
        }

        TenantDatabaseInfoReadModel? tenantInfo = await this.tenantReadRepository
            .GetDatabaseInfoByIdAsync(parsedTenantId, "customer", cancellationToken)
            .ConfigureAwait(false);

        if (tenantInfo is null)
        {
            this.logger.LogWarning(
                "On-demand tenant resolution returned no tenant metadata. TenantId={TenantId}",
                tenantId);
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
