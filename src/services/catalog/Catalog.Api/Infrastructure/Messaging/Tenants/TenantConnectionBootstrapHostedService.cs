// <copyright file="TenantConnectionBootstrapHostedService.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using FastEndpoints;
using Grpc.Core;
using SharedKernel.Core.Pricing;
using SharedKernel.Grpc.Contracts.Remote.V1.Tenants;
using SharedKernel.Persistence.Database.MultiTenant;

namespace Catalog.Api.Infrastructure.Messaging.Tenants;

/// <summary>
/// Bootstraps in-memory tenant connection mappings from Customer service at startup.
/// </summary>
internal sealed class TenantConnectionBootstrapHostedService(
    ILogger<TenantConnectionBootstrapHostedService> logger,
    WolverineTenantConnectionSource tenantConnectionSource,
    IVaultTenantConnectionProvider vaultTenantConnectionProvider)
    : IHostedService
{
    private readonly ILogger<TenantConnectionBootstrapHostedService> logger = logger;
    private readonly WolverineTenantConnectionSource tenantConnectionSource = tenantConnectionSource;
    private readonly IVaultTenantConnectionProvider vaultTenantConnectionProvider = vaultTenantConnectionProvider;

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        TenantConnectionSeedsRpcResult result;
        try
        {
            result = await new GetTenantConnectionSeedsCommand
            {
                ServiceName = "catalog",
            }.RemoteExecuteAsync(new CallOptions(cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            this.logger.LogWarning(exception, "Tenant bootstrap skipped because tenant seed RPC failed.");
            return;
        }

        if (result is null || !result.Succeeded)
        {
            this.logger.LogWarning(
                "Tenant bootstrap did not complete successfully. Detail={Detail}",
                result?.ErrorDetail ?? "No response from customer service.");
            return;
        }

        int mappedCount = 0;
        foreach (TenantConnectionSeedRpcItem item in result.Items)
        {
            if (!Guid.TryParse(item.TenantId, out Guid tenantId))
            {
                continue;
            }

            string connectionString;
            try
            {
                connectionString = await ResolveConnectionStringAsync(item, tenantId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                this.logger.LogWarning(
                    exception,
                    "Skipping tenant bootstrap for TenantId={TenantId}.",
                    item.TenantId);
                continue;
            }

            await this.tenantConnectionSource
                .AddTenantAsync(tenantId.ToString("D"), connectionString)
                .ConfigureAwait(false);

            mappedCount++;
        }

        if (this.logger.IsEnabled(LogLevel.Information))
        {
            this.logger.LogInformation(
                "Tenant bootstrap completed for catalog service. Loaded={MappedCount}; Total={TotalCount}",
                mappedCount,
                result.Items.Count);
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    private async Task<string> ResolveConnectionStringAsync(
        TenantConnectionSeedRpcItem item,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        DatabaseStrategy.TryFromName(
            item.DatabaseStrategy,
            ignoreCase: true,
            out DatabaseStrategy? strategy);

        DatabaseStrategy effectiveStrategy = strategy ?? DatabaseStrategy.Shared;
        if (effectiveStrategy == DatabaseStrategy.Shared)
        {
            return this.tenantConnectionSource.DefaultWriteConnectionString;
        }

        string preferredLookupKey = !string.IsNullOrWhiteSpace(item.Identifier)
            ? item.Identifier
            : tenantId.ToString("D");

        try
        {
            (string writeConnectionString, _) = await this.vaultTenantConnectionProvider
                .GetAsync(preferredLookupKey, cancellationToken)
                .ConfigureAwait(false);

            return writeConnectionString;
        }
        catch (TenantConnectionNotFoundException) when (!string.Equals(preferredLookupKey, tenantId.ToString("D"), StringComparison.OrdinalIgnoreCase))
        {
            (string writeConnectionString, _) = await this.vaultTenantConnectionProvider
                .GetAsync(tenantId.ToString("D"), cancellationToken)
                .ConfigureAwait(false);

            return writeConnectionString;
        }
    }
}
