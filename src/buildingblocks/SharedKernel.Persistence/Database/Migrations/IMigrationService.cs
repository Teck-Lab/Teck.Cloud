#nullable enable

namespace SharedKernel.Persistence.Database.Migrations;

/// <summary>
/// Service for managing database migrations in multi-tenant environments.
/// </summary>
public interface IMigrationService
{
    /// <summary>
    /// Applies migrations to a specific tenant's database.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Migration result.</returns>
    Task<MigrationResult> MigrateTenantDatabaseAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies migrations to the shared database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Migration result.</returns>
    Task<MigrationResult> MigrateSharedDatabaseAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies migrations to all tenant databases (shared + dedicated tenants).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of migration results for each tenant.</returns>
    Task<IReadOnlyCollection<MigrationResult>> MigrateAllDatabasesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a tenant's database needs migration.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if migrations are pending.</returns>
    Task<bool> HasPendingMigrationsAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current migration status for a tenant's database.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Migration status.</returns>
    Task<MigrationStatus> GetMigrationStatusAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a database migration operation.
/// </summary>
public sealed record MigrationResult
{
    /// <summary>
    /// Tenant identifier (null for shared database).
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// Indicates whether the migration succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Error message if migration failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Number of migrations applied.
    /// </summary>
    public int MigrationsApplied { get; init; }

    /// <summary>
    /// Duration of the migration operation.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Timestamp when the migration was performed.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// List of applied migration names.
    /// </summary>
    public IReadOnlyList<string>? AppliedMigrations { get; init; }

    public static MigrationResult CreateSuccess(
        string? tenantId,
        int migrationsApplied,
        TimeSpan duration,
        IReadOnlyList<string>? appliedMigrations = null) =>
        new()
        {
            TenantId = tenantId,
            Success = true,
            MigrationsApplied = migrationsApplied,
            Duration = duration,
            AppliedMigrations = appliedMigrations,
        };

    public static MigrationResult CreateFailure(
        string? tenantId,
        string errorMessage,
        TimeSpan duration) =>
        new()
        {
            TenantId = tenantId,
            Success = false,
            ErrorMessage = errorMessage,
            MigrationsApplied = 0,
            Duration = duration,
        };
}

/// <summary>
/// Migration status for a database.
/// </summary>
public sealed record MigrationStatus
{
    /// <summary>
    /// Tenant identifier.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Indicates whether the database exists.
    /// </summary>
    public required bool DatabaseExists { get; init; }

    /// <summary>
    /// List of pending migrations.
    /// </summary>
    public required IReadOnlyList<string> PendingMigrations { get; init; }

    /// <summary>
    /// List of applied migrations.
    /// </summary>
    public required IReadOnlyList<string> AppliedMigrations { get; init; }

    /// <summary>
    /// Last migration applied.
    /// </summary>
    public string? LastMigration { get; init; }

    /// <summary>
    /// Indicates whether migrations are pending.
    /// </summary>
    public bool HasPendingMigrations => PendingMigrations.Count > 0;
}
