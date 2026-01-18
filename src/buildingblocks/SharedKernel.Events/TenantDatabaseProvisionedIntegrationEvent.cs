using SharedKernel.Core.Events;

namespace SharedKernel.Events;

/// <summary>
/// Integration event raised when a new tenant database has been provisioned.
/// </summary>
public sealed class TenantDatabaseProvisionedIntegrationEvent : IntegrationEvent
{
    /// <summary>
    /// The tenant identifier.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// The database strategy (Shared, Dedicated, External).
    /// </summary>
    public required string DatabaseStrategy { get; init; }

    /// <summary>
    /// The database provider (PostgreSQL, SqlServer, MySQL).
    /// </summary>
    public required string DatabaseProvider { get; init; }

    /// <summary>
    /// Indicates whether the database has been created.
    /// </summary>
    public required bool DatabaseCreated { get; init; }

    /// <summary>
    /// Additional metadata about the provisioning.
    /// </summary>
    public Dictionary<string, string>? AdditionalMetadata { get; init; }
}
