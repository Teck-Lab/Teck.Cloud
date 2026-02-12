using SharedKernel.Core.Domain;


namespace Customer.Domain.Entities.TenantAggregate;

/// <summary>
/// Migration status for a tenant's service.
/// Tracks the progress of database migrations for each service.
/// </summary>
public class TenantMigrationStatus : BaseEntity
{
    /// <summary>
    /// Gets the tenant ID.
    /// </summary>
    public Guid TenantId { get; internal set; }

    /// <summary>
    /// Gets the service name (e.g., "catalog", "orders").
    /// </summary>
    public string ServiceName { get; internal set; } = default!;

    /// <summary>
    /// Gets the current migration status.
    /// </summary>
    public SharedKernel.Migration.Models.MigrationStatus Status { get; internal set; }

    /// <summary>
    /// Gets the last applied migration version/script name.
    /// </summary>
    public string? LastMigrationVersion { get; private set; }

    /// <summary>
    /// Gets the timestamp when migration started.
    /// </summary>
    public DateTime? StartedAt { get; private set; }

    /// <summary>
    /// Gets the timestamp when migration completed.
    /// </summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// Gets the error message if migration failed.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Gets the navigation property to tenant.
    /// </summary>
    public Tenant Tenant { get; private set; } = default!;

    internal TenantMigrationStatus() { } // Internal constructor for aggregate control

    /// <summary>
    /// Updates the migration status.
    /// </summary>
    /// <param name="status">The new migration status.</param>
    /// <param name="lastMigrationVersion">The last applied migration version.</param>
    /// <param name="errorMessage">The error message if migration failed.</param>
internal void UpdateStatus(
        SharedKernel.Migration.Models.MigrationStatus status,
        string? lastMigrationVersion,
        string? errorMessage)
    {
        var previousStatus = Status;
        Status = status;

        if (previousStatus == SharedKernel.Migration.Models.MigrationStatus.Pending && status == SharedKernel.Migration.Models.MigrationStatus.InProgress)
        {
            StartedAt = DateTime.UtcNow;
        }

        if (status == SharedKernel.Migration.Models.MigrationStatus.Completed || status == SharedKernel.Migration.Models.MigrationStatus.Failed)
        {
            CompletedAt = DateTime.UtcNow;
        }

        if (!string.IsNullOrEmpty(lastMigrationVersion))
        {
            LastMigrationVersion = lastMigrationVersion;
        }

        if (!string.IsNullOrEmpty(errorMessage))
        {
            ErrorMessage = errorMessage;
        }
    }
}
