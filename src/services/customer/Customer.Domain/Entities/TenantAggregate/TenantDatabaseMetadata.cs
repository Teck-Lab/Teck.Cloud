using SharedKernel.Core.Domain;

namespace Customer.Domain.Entities.TenantAggregate;

/// <summary>
/// Database metadata for a tenant's service.
/// Stores environment variable keys for runtime DSN resolution and read replica configuration.
/// </summary>
public class TenantDatabaseMetadata : BaseEntity
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
    /// Gets the environment variable key for write database connection string.
    /// Example: ConnectionStrings__Tenants__{tenantId}__Write.
    /// </summary>
    public string WriteEnvVarKey { get; internal set; } = default!;

    /// <summary>
    /// Gets the environment variable key for read database connection string (if separate).
    /// </summary>
    public string? ReadEnvVarKey { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether this tenant has a separate read database.
    /// </summary>
    public bool HasSeparateReadDatabase { get; internal set; }

    /// <summary>
    /// Gets the navigation property to tenant.
    /// </summary>
    public Tenant Tenant { get; private set; } = default!;

    internal TenantDatabaseMetadata() { } // Internal constructor for aggregate control
}
