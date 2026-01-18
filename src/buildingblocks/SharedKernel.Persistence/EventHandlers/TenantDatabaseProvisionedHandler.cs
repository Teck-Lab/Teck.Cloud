#nullable enable

using Microsoft.Extensions.Logging;
using SharedKernel.Events;
using SharedKernel.Persistence.Database.Migrations;

namespace SharedKernel.Persistence.EventHandlers;

/// <summary>
/// Example Wolverine handler for tenant database provisioning events.
/// This handler automatically runs migrations when a new tenant database is provisioned.
/// 
/// To use this handler in your service:
/// 1. Add a reference to SharedKernel.Persistence
/// 2. Create a similar handler in your Application layer (e.g., Catalog.Application/EventHandlers/IntegrationEvents/)
/// 3. Wolverine will automatically discover and register the handler
/// </summary>
public sealed class TenantDatabaseProvisionedHandler
{
    private readonly IMigrationService _migrationService;
    private readonly ILogger<TenantDatabaseProvisionedHandler> _logger;

    /// <inheritdoc/>
    public TenantDatabaseProvisionedHandler(
        IMigrationService migrationService,
        ILogger<TenantDatabaseProvisionedHandler> logger)
    {
        _migrationService = migrationService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the tenant database provisioned event by running migrations.
    /// This method is automatically invoked by Wolverine when the event is published.
    /// </summary>
    /// <param name="event">The integration event.</param>
    /// <returns>Task representing the async operation.</returns>
    public async Task Handle(TenantDatabaseProvisionedIntegrationEvent @event)
    {
        _logger.LogInformation(
            "Processing TenantDatabaseProvisionedIntegrationEvent for tenant {TenantId}, Strategy: {Strategy}, Provider: {Provider}",
            @event.TenantId,
            @event.DatabaseStrategy,
            @event.DatabaseProvider);

        // Only run migrations for dedicated or external databases
        // Shared databases are migrated on startup
        if (@event.DatabaseStrategy is "Dedicated" or "External")
        {
            if (!@event.DatabaseCreated)
            {
                _logger.LogWarning(
                    "Database for tenant {TenantId} has not been created yet, skipping migration",
                    @event.TenantId);
                return;
            }

            _logger.LogInformation(
                "Running migrations for tenant {TenantId} dedicated database",
                @event.TenantId);

            var result = await _migrationService.MigrateTenantDatabaseAsync(@event.TenantId);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Successfully migrated database for tenant {TenantId}. Applied {Count} migrations in {Duration}ms",
                    @event.TenantId,
                    result.MigrationsApplied,
                    result.Duration.TotalMilliseconds);
            }
            else
            {
                _logger.LogError(
                    "Failed to migrate database for tenant {TenantId}: {Error}",
                    @event.TenantId,
                    result.ErrorMessage);

                // Depending on your requirements, you might want to:
                // 1. Throw an exception to retry the message
                // 2. Publish a failure event
                // 3. Store the failure for manual intervention
                throw new InvalidOperationException(
                    $"Migration failed for tenant {@event.TenantId}: {result.ErrorMessage}");
            }
        }
        else
        {
            _logger.LogInformation(
                "Tenant {TenantId} uses shared database, skipping dedicated migration",
                @event.TenantId);
        }
    }
}
