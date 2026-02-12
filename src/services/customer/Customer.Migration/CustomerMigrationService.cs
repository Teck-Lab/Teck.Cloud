using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedKernel.Migration;
using SharedKernel.Migration.Models;
using SharedKernel.Migration.Services;
using SharedKernel.Secrets;

namespace Customer.Migration;

/// <summary>
/// Migration service for Customer database.
/// Runs on startup to ensure the Customer database is up to date.
/// </summary>
internal sealed class CustomerMigrationService : MigrationServiceBase
{
    private readonly IConfiguration _configuration;
    private readonly IHostApplicationLifetime _lifetime;

    public CustomerMigrationService(
        IVaultSecretsManager vaultSecretsManager,
        DbUpMigrationRunner migrationRunner,
        CustomerApiClient customerApiClient,
        IConfiguration configuration,
        IHostApplicationLifetime lifetime,
        ILogger<CustomerMigrationService> logger)
        : base("customer", vaultSecretsManager, migrationRunner, customerApiClient, logger)
    {
        _configuration = configuration;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("Customer Migration Service starting...");

        try
        {
            // Get migration configuration
            var provider = _configuration["Database:Provider"] ?? "PostgreSQL";
            var scriptsPath = _configuration["Migration:ScriptsPath"] ?? "./Scripts";

            Logger.LogInformation(
                "Starting migration for Customer service. Provider: {Provider}, Scripts path: {ScriptsPath}",
                provider,
                scriptsPath);

            // Create migration options
            var options = new MigrationOptions
            {
                ScriptsPath = scriptsPath,
                Provider = provider,
                JournalSchema = _configuration["Migration:JournalSchema"],
                JournalTable = _configuration["Migration:JournalTable"] ?? "SchemaVersions"
            };

            // Run migration for the shared customer database
            var result = await MigrateSharedDatabaseAsync(provider, options, stoppingToken);

            if (result.Success)
            {
                Logger.LogInformation(
                    "Customer database migration completed successfully. Applied {Count} scripts in {Duration}ms",
                    result.ScriptsApplied,
                    result.Duration.TotalMilliseconds);

                // Stop the application gracefully after successful migration
                _lifetime.StopApplication();
            }
            else
            {
                Logger.LogError(
                    "Customer database migration failed: {Error}",
                    result.ErrorMessage);

                // Exit with error code
                Environment.ExitCode = 1;
                _lifetime.StopApplication();
            }
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Fatal error during Customer database migration");
            Environment.ExitCode = 1;
            _lifetime.StopApplication();
        }
    }
}
