// <copyright file="TenantCreatedIntegrationConsumer.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using SharedKernel.Core.Pricing;
using SharedKernel.Events;
using SharedKernel.Persistence.Database.MultiTenant;

namespace Catalog.Infrastructure.Messaging.Tenants;

/// <summary>
/// Consumes tenant-created integration events and registers tenant message-store connections for Wolverine.
/// </summary>
public sealed class TenantCreatedIntegrationConsumer
{
    private readonly WolverineTenantConnectionSource tenantConnectionSource;
    private readonly IVaultTenantConnectionProvider vaultTenantConnectionProvider;
    private readonly ILogger<TenantCreatedIntegrationConsumer> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantCreatedIntegrationConsumer"/> class.
    /// </summary>
    /// <param name="tenantConnectionSource">The Wolverine tenant source.</param>
    /// <param name="vaultTenantConnectionProvider">The Vault connection provider.</param>
    /// <param name="logger">The logger.</param>
    public TenantCreatedIntegrationConsumer(
        WolverineTenantConnectionSource tenantConnectionSource,
        IVaultTenantConnectionProvider vaultTenantConnectionProvider,
        ILogger<TenantCreatedIntegrationConsumer> logger)
    {
        this.tenantConnectionSource = tenantConnectionSource;
        this.vaultTenantConnectionProvider = vaultTenantConnectionProvider;
        this.logger = logger;
    }

    /// <summary>
    /// Registers tenant message-store connections for new tenants.
    /// </summary>
    /// <param name="integrationEvent">The tenant created event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Handle(TenantCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        string tenantId = integrationEvent.TenantId.ToString("D");
        string connectionString = await ResolveConnectionStringAsync(integrationEvent, cancellationToken).ConfigureAwait(false);

        await this.tenantConnectionSource
            .AddTenantAsync(tenantId, connectionString)
            .ConfigureAwait(false);

        if (this.logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
        {
            this.logger.LogInformation(
                "Registered Wolverine tenant connection for tenant {TenantId} using strategy {DatabaseStrategy}",
                tenantId,
                integrationEvent.DatabaseStrategy);
        }
    }

    private async Task<string> ResolveConnectionStringAsync(
        TenantCreatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        DatabaseStrategy.TryFromName(
            integrationEvent.DatabaseStrategy,
            ignoreCase: true,
            out DatabaseStrategy? strategy);

        DatabaseStrategy effectiveStrategy = strategy ?? DatabaseStrategy.Shared;
        if (effectiveStrategy == DatabaseStrategy.Shared)
        {
            return this.tenantConnectionSource.DefaultWriteConnectionString;
        }

        string vaultLookupKey = !string.IsNullOrWhiteSpace(integrationEvent.Identifier)
            ? integrationEvent.Identifier
            : integrationEvent.TenantId.ToString("D");

        (string writeConnectionString, _) = await this.vaultTenantConnectionProvider
            .GetAsync(vaultLookupKey, cancellationToken)
            .ConfigureAwait(false);

        return writeConnectionString;
    }
}
