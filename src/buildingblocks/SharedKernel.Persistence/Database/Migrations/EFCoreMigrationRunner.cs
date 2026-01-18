#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace SharedKernel.Persistence.Database.Migrations;

/// <summary>
/// Base implementation of database migration runner using Entity Framework Core.
/// </summary>
/// <typeparam name="TDbContext">The database context type.</typeparam>
public sealed class EFCoreMigrationRunner<TDbContext> where TDbContext : DbContext
{
    private readonly ILogger<EFCoreMigrationRunner<TDbContext>> _logger;

    public EFCoreMigrationRunner(ILogger<EFCoreMigrationRunner<TDbContext>> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Applies pending migrations to the database.
    /// </summary>
    /// <param name="context">Database context.</param>
    /// <param name="tenantId">Tenant identifier (null for shared database).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Migration result.</returns>
    public async Task<MigrationResult> ApplyMigrationsAsync(
        TDbContext context,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Starting migration for {TenantType} {TenantId}",
                tenantId is null ? "shared database" : "tenant",
                tenantId ?? "N/A");

            // Get pending migrations
            var pendingMigrations = (await context.Database
                .GetPendingMigrationsAsync(cancellationToken))
                .ToList();

            if (pendingMigrations.Count == 0)
            {
                _logger.LogInformation(
                    "No pending migrations for {TenantType} {TenantId}",
                    tenantId is null ? "shared database" : "tenant",
                    tenantId ?? "N/A");

                stopwatch.Stop();
                return MigrationResult.CreateSuccess(
                    tenantId,
                    migrationsApplied: 0,
                    stopwatch.Elapsed);
            }

            _logger.LogInformation(
                "Found {Count} pending migrations for {TenantType} {TenantId}: {Migrations}",
                pendingMigrations.Count,
                tenantId is null ? "shared database" : "tenant",
                tenantId ?? "N/A",
                string.Join(", ", pendingMigrations));

            // Apply migrations
            await context.Database.MigrateAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "Successfully applied {Count} migrations for {TenantType} {TenantId} in {Duration}ms",
                pendingMigrations.Count,
                tenantId is null ? "shared database" : "tenant",
                tenantId ?? "N/A",
                stopwatch.ElapsedMilliseconds);

            return MigrationResult.CreateSuccess(
                tenantId,
                migrationsApplied: pendingMigrations.Count,
                stopwatch.Elapsed,
                appliedMigrations: pendingMigrations);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Failed to apply migrations for {TenantType} {TenantId} after {Duration}ms",
                tenantId is null ? "shared database" : "tenant",
                tenantId ?? "N/A",
                stopwatch.ElapsedMilliseconds);

            return MigrationResult.CreateFailure(
                tenantId,
                errorMessage: ex.Message,
                stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Gets the migration status for the database.
    /// </summary>
    /// <param name="context">Database context.</param>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Migration status.</returns>
    public async Task<MigrationStatus> GetMigrationStatusAsync(
        TDbContext context,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var databaseExists = await context.Database.CanConnectAsync(cancellationToken);

            var pendingMigrations = databaseExists
                ? (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList()
                : new List<string>();

            var appliedMigrations = databaseExists
                ? (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList()
                : new List<string>();

            return new MigrationStatus
            {
                TenantId = tenantId,
                DatabaseExists = databaseExists,
                PendingMigrations = pendingMigrations,
                AppliedMigrations = appliedMigrations,
                LastMigration = appliedMigrations.Count > 0 ? appliedMigrations[^1] : null,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to get migration status for tenant {TenantId}",
                tenantId);

            return new MigrationStatus
            {
                TenantId = tenantId,
                DatabaseExists = false,
                PendingMigrations = Array.Empty<string>(),
                AppliedMigrations = Array.Empty<string>(),
            };
        }
    }

    /// <summary>
    /// Checks if there are pending migrations.
    /// </summary>
    /// <param name="context">Database context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if migrations are pending.</returns>
    public async Task<bool> HasPendingMigrationsAsync(
        TDbContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await context.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return true; // Database doesn't exist, migrations needed
            }

            var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);
            return pendingMigrations.Any();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check pending migrations");
            throw;
        }
    }
}
