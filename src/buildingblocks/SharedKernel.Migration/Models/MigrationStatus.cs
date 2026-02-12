namespace SharedKernel.Migration.Models;

/// <summary>
/// Status of a tenant migration.
/// </summary>
public enum MigrationStatus
{
    /// <summary>
    /// Migration is pending.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Migration is in progress.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Migration completed successfully.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Migration failed.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Some services completed but others failed (partially provisioned).
    /// </summary>
    PartiallyProvisioned = 4,
}
