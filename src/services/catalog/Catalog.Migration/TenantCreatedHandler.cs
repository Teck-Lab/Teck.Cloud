using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedKernel.Events;
using SharedKernel.Migration;
using SharedKernel.Migration.Models;
using SharedKernel.Migration.Services;

namespace Catalog.Migration;

/// <summary>
/// Handles TenantCreatedIntegrationEvent to trigger catalog database migration for new tenants.
/// </summary>
internal sealed class TenantCreatedHandler
{
    private readonly DbUpMigrationRunner _migrationRunner;
    private readonly CustomerApiClient _customerApiClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantCreatedHandler> _logger;

    public TenantCreatedHandler(
        DbUpMigrationRunner migrationRunner,
        CustomerApiClient customerApiClient,
        IConfiguration configuration,
        ILogger<TenantCreatedHandler> logger)
    {
        _migrationRunner = migrationRunner;
        _customerApiClient = customerApiClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task Handle(TenantCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Received TenantCreatedIntegrationEvent for tenant {TenantId} ({Identifier})",
            integrationEvent.TenantId,
            integrationEvent.Identifier);

        try
        {
            // Update status to InProgress
            await _customerApiClient.UpdateMigrationStatusAsync(
                integrationEvent.TenantId.ToString(),
                "catalog",
                MigrationStatus.InProgress,
                cancellationToken: cancellationToken);

            // Get database info from Customer API
            var dbInfo = await _customerApiClient.GetServiceDatabaseInfoAsync(
                integrationEvent.TenantId.ToString(),
                "catalog",
                cancellationToken);

            if (dbInfo == null)
            {
                _logger.LogError(
                    "Could not retrieve database info for tenant {TenantId}, service catalog",
                    integrationEvent.TenantId);

                await _customerApiClient.UpdateMigrationStatusAsync(
                    integrationEvent.TenantId.ToString(),
                    "catalog",
                    MigrationStatus.Failed,
                    errorMessage: "Database metadata not found",
                    cancellationToken: cancellationToken);
                return;
            }

            // Get migration configuration
            var scriptsPath = _configuration["Migration:ScriptsPath"] ?? "./Scripts";

            // Create migration options
            var options = new MigrationOptions
            {
                ScriptsPath = scriptsPath,
                Provider = integrationEvent.DatabaseProvider,
                JournalSchema = _configuration["Migration:JournalSchema"],
                JournalTable = _configuration["Migration:JournalTable"] ?? "SchemaVersions"
            };

            // Run migration
            var result = await _migrationRunner.MigrateAsync(
                dbInfo.VaultWritePath,
                options,
                cancellationToken);

            // Update status based on result
            if (result.Success)
            {
                var lastScript = result.AppliedScripts.Count > 0
                    ? result.AppliedScripts[^1]
                    : null;

                await _customerApiClient.UpdateMigrationStatusAsync(
                    integrationEvent.TenantId.ToString(),
                    "catalog",
                    MigrationStatus.Completed,
                    lastMigrationVersion: lastScript,
                    cancellationToken: cancellationToken);

                _logger.LogInformation(
                    "Successfully migrated catalog database for tenant {TenantId}. Applied {Count} scripts",
                    integrationEvent.TenantId,
                    result.ScriptsApplied);
            }
            else
            {
                await _customerApiClient.UpdateMigrationStatusAsync(
                    integrationEvent.TenantId.ToString(),
                    "catalog",
                    MigrationStatus.Failed,
                    errorMessage: result.ErrorMessage,
                    cancellationToken: cancellationToken);

                _logger.LogError(
                    "Failed to migrate catalog database for tenant {TenantId}. Error: {Error}",
                    integrationEvent.TenantId,
                    result.ErrorMessage);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Error processing TenantCreatedIntegrationEvent for tenant {TenantId}",
                integrationEvent.TenantId);

            await _customerApiClient.UpdateMigrationStatusAsync(
                integrationEvent.TenantId.ToString(),
                "catalog",
                MigrationStatus.Failed,
                errorMessage: exception.Message,
                cancellationToken: cancellationToken);
        }
    }
}
