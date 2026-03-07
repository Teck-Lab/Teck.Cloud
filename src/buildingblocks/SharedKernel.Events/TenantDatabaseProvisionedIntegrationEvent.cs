using SharedKernel.Core.Events;

namespace SharedKernel.Events;

/// <summary>
/// Integration event raised when a new tenant database has been provisioned.
/// </summary>
public sealed class TenantDatabaseProvisionedIntegrationEvent : IntegrationEvent
{
    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the database strategy (Shared, Dedicated, External).
    /// </summary>
    public required string DatabaseStrategy { get; init; }

    /// <summary>
    /// Gets the database provider (PostgreSQL, SqlServer, MySQL).
    /// </summary>
    public required string DatabaseProvider { get; init; }

    /// <summary>
    /// Gets a value indicating whether the database has been created.
    /// </summary>
    public required bool DatabaseCreated { get; init; }

    /// <summary>
    /// Gets additional metadata about the provisioning.
    /// </summary>
    public IReadOnlyDictionary<string, string>? AdditionalMetadata { get; init; }
}
