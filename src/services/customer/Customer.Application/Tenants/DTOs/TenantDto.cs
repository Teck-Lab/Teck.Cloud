using SharedKernel.Migration.Models;

namespace Customer.Application.Tenants.DTOs;

/// <summary>
/// Data transfer object for Tenant.
/// </summary>
public record TenantDto
{
    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the tenant identifier (unique name/slug).
    /// </summary>
    public string Identifier { get; init; } = default!;

    /// <summary>
    /// Gets the tenant display name.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// Gets the tenant plan.
    /// </summary>
    public string Plan { get; init; } = default!;

    /// <summary>
    /// Gets the database strategy.
    /// </summary>
    public string DatabaseStrategy { get; init; } = default!;

    /// <summary>
    /// Gets the database provider.
    /// </summary>
    public string DatabaseProvider { get; init; } = default!;

    /// <summary>
    /// Gets a value indicating whether the tenant is active.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Gets the database metadata for each service.
    /// </summary>
    public IReadOnlyCollection<TenantDatabaseMetadataDto> Databases { get; init; } = Array.Empty<TenantDatabaseMetadataDto>();

    /// <summary>
    /// Gets the migration statuses for each service.
    /// </summary>
    public IReadOnlyCollection<TenantMigrationStatusDto> MigrationStatuses { get; init; } = Array.Empty<TenantMigrationStatusDto>();

    /// <summary>
    /// Gets the creation date.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the last update date.
    /// </summary>
    public DateTimeOffset? UpdatedOn { get; init; }
}

/// <summary>
/// Data transfer object for TenantDatabaseMetadata.
/// </summary>
public record TenantDatabaseMetadataDto
{
    /// <summary>
    /// Gets the service name.
    /// </summary>
    public string ServiceName { get; init; } = default!;

    /// <summary>
    /// Gets the Vault path for write database credentials.
    /// </summary>
    public string VaultWritePath { get; init; } = default!;

    /// <summary>
    /// Gets the Vault path for read database credentials.
    /// </summary>
    public string? VaultReadPath { get; init; }

    /// <summary>
    /// Gets a value indicating whether this service has a separate read database.
    /// </summary>
    public bool HasSeparateReadDatabase { get; init; }
}

/// <summary>
/// Data transfer object for TenantMigrationStatus.
/// </summary>
public record TenantMigrationStatusDto
{
    /// <summary>
    /// Gets the service name.
    /// </summary>
    public string ServiceName { get; init; } = default!;

    /// <summary>
    /// Gets the migration status.
    /// </summary>
    public MigrationStatus Status { get; init; }

    /// <summary>
    /// Gets the last migration version applied.
    /// </summary>
    public string? LastMigrationVersion { get; init; }

    /// <summary>
    /// Gets the time when the migration started.
    /// </summary>
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// Gets the time when the migration completed.
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Gets the error message if the migration failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
