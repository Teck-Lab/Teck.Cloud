using Catalog.Infrastructure.Persistence;
using JasperFx;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using SharedKernel.Core.Pricing;
using SharedKernel.Persistence.Database.Migrations;
using SharedKernel.Persistence.Database.MultiTenant;
using SharedKernel.Secrets;

namespace Catalog.Migrator;

/// <summary>
/// Database migration console application for the Catalog service.
///
/// This application is designed to run as:
/// 1. Kubernetes Job (pre-deployment) - Migrates shared database and all tenant databases.
/// 2. ArgoCD PreSync Hook - Ensures migrations before service deployment.
/// 3. Manual execution - For troubleshooting or ad-hoc migrations.
///
/// Usage:
///   dotnet run                           # Migrate all databases.
///   dotnet run -- --shared-only          # Migrate shared database only.
///   dotnet run -- --tenant {tenantId}    # Migrate specific tenant.
/// </summary>
internal sealed class Program
{
    /// <summary>
    /// Main entry point for the migration application.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code (0 for success, 1 for failure).</returns>
#pragma warning disable CA1052 // Program class is required for ILogger<Program>
    public static async Task<int> Main(string[] args)
#pragma warning restore CA1052
    {
        // Configure Serilog for console output
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            Log.Information("==============================================");
            Log.Information("Catalog Database Migrator Starting");
            Log.Information("==============================================");

            var host = CreateHostBuilder(args).Build();

            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;

            var migrationService = services.GetRequiredService<IMigrationService>();
            var logger = services.GetRequiredService<ILogger<Program>>();

            // Parse command-line arguments
            var mode = DetermineMigrationMode(args);

            var success = mode switch
            {
                MigrationMode.SharedOnly => await MigrateSharedOnlyAsync(migrationService, logger),
                MigrationMode.SpecificTenant => await MigrateSpecificTenantAsync(migrationService, logger, args),
                MigrationMode.All => await MigrateAllAsync(migrationService, logger),
                _ => throw new InvalidOperationException($"Unknown migration mode: {mode}"),
            };

            if (success)
            {
                Log.Information("==============================================");
                Log.Information("Migration completed successfully");
                Log.Information("==============================================");
                return 0;
            }

            Log.Error("==============================================");
            Log.Error("Migration failed");
            Log.Error("==============================================");
            return 1;
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "Migration failed with fatal error");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true)
                    .AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;

                // Get connection strings
                var catalogConnectionString = configuration["ConnectionStrings:catalogdb"]
                    ?? throw new InvalidOperationException("Connection string 'catalogdb' not found");

                // Add Vault secrets management
                services.AddVaultSecretsManagement(configuration);

                // Add multi-tenant support (minimal configuration for migrations)
                services.AddMemoryCache();
                services.AddHttpClient();

                // Add tenant connection resolver
                services.AddSingleton<ITenantDbConnectionResolver>(sp =>
                    new TenantDbConnectionResolver(
                        sp,
                        catalogConnectionString,
                        catalogConnectionString,
                        DatabaseProvider.PostgreSQL));

                // Add migration services
                services.AddMultiTenantMigrations<ApplicationWriteDbContext>(
                    DatabaseProvider.PostgreSQL);

                // Add DbContext (required for migrations)
                services.AddDbContext<ApplicationWriteDbContext>();
            })
            .UseSerilog();
    private static MigrationMode DetermineMigrationMode(string[] args)
    {
        if (args.Contains("--shared-only", StringComparer.OrdinalIgnoreCase))
        {
            return MigrationMode.SharedOnly;
        }

        if (args.Contains("--tenant", StringComparer.OrdinalIgnoreCase))
        {
            return MigrationMode.SpecificTenant;
        }

        return MigrationMode.All;
    }

    private static async Task<bool> MigrateSharedOnlyAsync(
        IMigrationService migrationService,
        ILogger<Program> logger)
    {
        logger.LogInformation("Running migration for SHARED DATABASE only");

        var result = await migrationService.MigrateSharedDatabaseAsync();

        if (result.Success)
        {
            logger.LogInformation(
                "✓ Shared database migrated successfully. Applied {Count} migrations in {Duration:F2}s",
                result.MigrationsApplied,
                result.Duration.TotalSeconds);
            return true;
        }
        else
        {
            logger.LogError(
                "✗ Shared database migration failed: {Error}",
                result.ErrorMessage);
            return false;
        }
    }

    private static async Task<bool> MigrateSpecificTenantAsync(
        IMigrationService migrationService,
        ILogger<Program> logger,
        string[] args)
    {
        var tenantIdIndex = Array.IndexOf(args, "--tenant") + 1;
        if (tenantIdIndex >= args.Length)
        {
            logger.LogError("--tenant flag requires a tenant ID argument");
            return false;
        }

        var tenantId = args[tenantIdIndex];
        logger.LogInformation("Running migration for TENANT: {TenantId}", tenantId);

        var result = await migrationService.MigrateTenantDatabaseAsync(tenantId);

        if (result.Success)
        {
            logger.LogInformation(
                "✓ Tenant {TenantId} migrated successfully. Applied {Count} migrations in {Duration:F2}s",
                tenantId,
                result.MigrationsApplied,
                result.Duration.TotalSeconds);
            return true;
        }
        else
        {
            logger.LogError(
                "✗ Tenant {TenantId} migration failed: {Error}",
                tenantId,
                result.ErrorMessage);
            return false;
        }
    }

    private static async Task<bool> MigrateAllAsync(
        IMigrationService migrationService,
        ILogger<Program> logger)
    {
        logger.LogInformation("Running migration for ALL DATABASES (shared + all tenants)");

        var results = await migrationService.MigrateAllDatabasesAsync();

        var successCount = 0;
        var failureCount = 0;

        foreach (var result in results)
        {
            var database = result.TenantId ?? "SHARED";

            if (result.Success)
            {
                logger.LogInformation(
                    "✓ {Database}: Applied {Count} migrations in {Duration:F2}s",
                    database,
                    result.MigrationsApplied,
                    result.Duration.TotalSeconds);
                successCount++;
            }
            else
            {
                logger.LogError(
                    "✗ {Database}: Migration failed - {Error}",
                    database,
                    result.ErrorMessage);
                failureCount++;
            }
        }

        logger.LogInformation("");
        logger.LogInformation("Migration Summary:");
        logger.LogInformation("  Successful: {SuccessCount}", successCount);
        logger.LogInformation("  Failed:     {FailureCount}", failureCount);
        logger.LogInformation("  Total:      {TotalCount}", results.Count);

        return failureCount == 0;
    }

    private enum MigrationMode
    {
        All,
        SharedOnly,
        SpecificTenant
    }
}
