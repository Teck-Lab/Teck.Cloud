using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedKernel.Migration.Models;
using SharedKernel.Migration.Services;
using SharedKernel.Secrets;

namespace SharedKernel.Migration;

/// <summary>
/// Base class for migration services that handle tenant database migrations.
/// Designed to be extended by service-specific migration services.
/// </summary>
public abstract class MigrationServiceBase : BackgroundService
{
    protected readonly IVaultSecretsManager VaultSecretsManager;
    protected readonly DbUpMigrationRunner MigrationRunner;
    protected readonly CustomerApiClient CustomerApiClient;
    protected readonly ILogger Logger;
    protected readonly string ServiceName;

    protected MigrationServiceBase(
        string serviceName,
        IVaultSecretsManager vaultSecretsManager,
        DbUpMigrationRunner migrationRunner,
        CustomerApiClient customerApiClient,
        ILogger logger)
    {
        ServiceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
        VaultSecretsManager = vaultSecretsManager ?? throw new ArgumentNullException(nameof(vaultSecretsManager));
        MigrationRunner = migrationRunner ?? throw new ArgumentNullException(nameof(migrationRunner));
        CustomerApiClient = customerApiClient ?? throw new ArgumentNullException(nameof(customerApiClient));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Migrates a tenant's database using the provided vault path.
    /// </summary>
    protected async Task<MigrationResult> MigrateTenantDatabaseAsync(
        string tenantId,
        string vaultPath,
        MigrationOptions options,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation(
            "Starting migration for tenant {TenantId}, service {ServiceName}, vault path {VaultPath}",
            tenantId, ServiceName, vaultPath);

        // Update status to InProgress
        await CustomerApiClient.UpdateMigrationStatusAsync(
            tenantId,
            ServiceName,
            MigrationStatus.InProgress,
            cancellationToken: cancellationToken);

        // Run migration
        var result = await MigrationRunner.MigrateAsync(vaultPath, options, cancellationToken);

        // Update status based on result
        if (result.Success)
        {
            await CustomerApiClient.UpdateMigrationStatusAsync(
                tenantId,
                ServiceName,
                MigrationStatus.Completed,
                lastMigrationVersion: result.AppliedScripts.LastOrDefault(),
                cancellationToken: cancellationToken);

            Logger.LogInformation(
                "Successfully migrated tenant {TenantId}, service {ServiceName}. Applied {Count} scripts",
                tenantId, ServiceName, result.ScriptsApplied);
        }
        else
        {
            await CustomerApiClient.UpdateMigrationStatusAsync(
                tenantId,
                ServiceName,
                MigrationStatus.Failed,
                errorMessage: result.ErrorMessage,
                cancellationToken: cancellationToken);

            Logger.LogError(
                "Failed to migrate tenant {TenantId}, service {ServiceName}. Error: {Error}",
                tenantId, ServiceName, result.ErrorMessage);
        }

        return result;
    }

    /// <summary>
    /// Migrates the shared database for this service.
    /// </summary>
    protected async Task<MigrationResult> MigrateSharedDatabaseAsync(
        string provider,
        MigrationOptions options,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation(
            "Starting shared database migration for service {ServiceName}, provider {Provider}",
            ServiceName, provider);

        var vaultPath = $"database/shared/{provider.ToLowerInvariant()}/{ServiceName}/write";

        var result = await MigrationRunner.MigrateAsync(vaultPath, options, cancellationToken);

        if (result.Success)
        {
            Logger.LogInformation(
                "Successfully migrated shared database for service {ServiceName}. Applied {Count} scripts",
                ServiceName, result.ScriptsApplied);
        }
        else
        {
            Logger.LogError(
                "Failed to migrate shared database for service {ServiceName}. Error: {Error}",
                ServiceName, result.ErrorMessage);
        }

        return result;
    }

    /// <summary>
    /// Gets the database info for a tenant from the Customer API.
    /// </summary>
    protected async Task<ServiceDatabaseInfo?> GetTenantDatabaseInfoAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        Logger.LogDebug(
            "Getting database info for tenant {TenantId}, service {ServiceName}",
            tenantId, ServiceName);

        var info = await CustomerApiClient.GetServiceDatabaseInfoAsync(
            tenantId,
            ServiceName,
            cancellationToken);

        if (info is null)
        {
            Logger.LogWarning(
                "Could not retrieve database info for tenant {TenantId}, service {ServiceName}",
                tenantId, ServiceName);
        }

        return info;
    }
}
