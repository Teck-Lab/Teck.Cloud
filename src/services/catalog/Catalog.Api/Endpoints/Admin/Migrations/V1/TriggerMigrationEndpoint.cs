using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using SharedKernel.Persistence.Database.Migrations;

namespace Catalog.Api.Endpoints.Admin.Migrations.V1;

/// <summary>
/// Request to trigger database migration for a tenant.
/// </summary>
public sealed record TriggerMigrationRequest
{
    /// <summary>
    /// The tenant identifier. If null, migrates shared database.
    /// </summary>
    public string? TenantId { get; init; }
}

/// <summary>
/// Response containing migration results.
/// </summary>
public sealed record TriggerMigrationResponse
{
    /// <summary>
    /// Gets a value indicating whether the migration was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the tenant identifier. Null for shared database migrations.
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// Gets the number of migrations that were applied.
    /// </summary>
    public int MigrationsApplied { get; init; }

    /// <summary>
    /// Gets the duration of the migration operation in seconds.
    /// </summary>
    public double DurationSeconds { get; init; }

    /// <summary>
    /// Gets the error message if the migration failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the list of migrations that were applied.
    /// </summary>
    public IReadOnlyList<string>? AppliedMigrations { get; init; }
}

/// <summary>
/// Endpoint for triggering database migrations at runtime.
/// This is used when new tenant databases are provisioned after deployment.
/// </summary>
/// <remarks>
/// Requires admin role and is intended for operational use only.
/// </remarks>
internal sealed class TriggerMigrationEndpoint : Endpoint<TriggerMigrationRequest, Results<Ok<TriggerMigrationResponse>, ProblemHttpResult>>
{
    private readonly IMigrationService _migrationService;
    private readonly ILogger<TriggerMigrationEndpoint> _logger;

    public TriggerMigrationEndpoint(
        IMigrationService migrationService,
        ILogger<TriggerMigrationEndpoint> logger)
    {
        _migrationService = migrationService;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/admin/migrations/trigger");
        
        // Require admin role - adjust based on your authorization setup
        Roles("admin", "system-admin");
        
        // Versioning
        Version(1);
        
        // OpenAPI documentation
        Summary(s =>
        {
            s.Summary = "Trigger database migration for a tenant";
            s.Description = "Runs database migrations for a specific tenant or shared database. Used for runtime tenant provisioning.";
            s.ExampleRequest = new TriggerMigrationRequest { TenantId = "tenant-123" };
        });
    }

    public override async Task<Results<Ok<TriggerMigrationResponse>, ProblemHttpResult>> ExecuteAsync(
        TriggerMigrationRequest req,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation(
                "Migration triggered for {TenantType}: {TenantId}",
                req.TenantId is null ? "shared database" : "tenant",
                req.TenantId ?? "N/A");

            MigrationResult result;

            if (string.IsNullOrWhiteSpace(req.TenantId))
            {
                // Migrate shared database
                result = await _migrationService.MigrateSharedDatabaseAsync(ct);
            }
            else
            {
                // Migrate tenant database
                result = await _migrationService.MigrateTenantDatabaseAsync(req.TenantId, ct);
            }

            var response = new TriggerMigrationResponse
            {
                Success = result.Success,
                TenantId = result.TenantId,
                MigrationsApplied = result.MigrationsApplied,
                DurationSeconds = result.Duration.TotalSeconds,
                ErrorMessage = result.ErrorMessage,
                AppliedMigrations = result.AppliedMigrations,
            };

            if (result.Success)
            {
                _logger.LogInformation(
                    "Migration completed successfully for {TenantId}. Applied {Count} migrations in {Duration:F2}s",
                    result.TenantId ?? "shared",
                    result.MigrationsApplied,
                    result.Duration.TotalSeconds);

                return TypedResults.Ok(response);
            }
            else
            {
                _logger.LogError(
                    "Migration failed for {TenantId}: {Error}",
                    result.TenantId ?? "shared",
                    result.ErrorMessage);

                return TypedResults.Problem(
                    title: "Migration Failed",
                    detail: result.ErrorMessage,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during migration for {TenantId}", req.TenantId);

            return TypedResults.Problem(
                title: "Migration Error",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
