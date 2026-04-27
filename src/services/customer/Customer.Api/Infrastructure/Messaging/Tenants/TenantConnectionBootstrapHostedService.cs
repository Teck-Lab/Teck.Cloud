// <copyright file="TenantConnectionBootstrapHostedService.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Customer.Application.Tenants.ReadModels;
using Customer.Application.Tenants.Repositories;
using SharedKernel.Core.Pricing;
using SharedKernel.Persistence.Database.MultiTenant;

namespace Customer.Api.Infrastructure.Messaging.Tenants;

/// <summary>
/// Bootstraps in-memory tenant connection mappings from Customer tenant records at startup.
/// </summary>
internal sealed class TenantConnectionBootstrapHostedService(
    ILogger<TenantConnectionBootstrapHostedService> logger,
    ITenantReadRepository tenantReadRepository,
    WolverineTenantConnectionSource tenantConnectionSource,
    IVaultTenantConnectionProvider vaultTenantConnectionProvider)
    : IHostedService
{
    private readonly ILogger<TenantConnectionBootstrapHostedService> logger = logger;
    private readonly ITenantReadRepository tenantReadRepository = tenantReadRepository;
    private readonly WolverineTenantConnectionSource tenantConnectionSource = tenantConnectionSource;
    private readonly IVaultTenantConnectionProvider vaultTenantConnectionProvider = vaultTenantConnectionProvider;

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<TenantConnectionSeedReadModel> seeds = await this.tenantReadRepository
            .ListConnectionSeedsAsync(cancellationToken)
            .ConfigureAwait(false);

        int mappedCount = 0;
        foreach (TenantConnectionSeedReadModel item in seeds)
        {
            string connectionString;
            try
            {
                connectionString = await ResolveConnectionStringAsync(item, cancellationToken).ConfigureAwait(false);
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
                .AddTenantAsync(item.TenantId.ToString("D"), connectionString)
                .ConfigureAwait(false);

            mappedCount++;
        }

        if (this.logger.IsEnabled(LogLevel.Information))
        {
            this.logger.LogInformation(
                "Tenant bootstrap completed for customer service. Loaded={MappedCount}; Total={TotalCount}",
                mappedCount,
                seeds.Count);
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    private async Task<string> ResolveConnectionStringAsync(TenantConnectionSeedReadModel item, CancellationToken cancellationToken)
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
            : item.TenantId.ToString("D");

        try
        {
            (string writeConnectionString, _) = await this.vaultTenantConnectionProvider
                .GetAsync(preferredLookupKey, cancellationToken)
                .ConfigureAwait(false);

            return writeConnectionString;
        }
        catch (TenantConnectionNotFoundException) when (!string.Equals(preferredLookupKey, item.TenantId.ToString("D"), StringComparison.OrdinalIgnoreCase))
        {
            (string writeConnectionString, _) = await this.vaultTenantConnectionProvider
                .GetAsync(item.TenantId.ToString("D"), cancellationToken)
                .ConfigureAwait(false);

            return writeConnectionString;
        }
    }
}
