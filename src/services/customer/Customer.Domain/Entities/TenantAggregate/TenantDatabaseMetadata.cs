using SharedKernel.Core.Domain;

namespace Customer.Domain.Entities.TenantAggregate;

/// <summary>
/// Database metadata for a tenant's service.
/// Stores Vault paths and read replica configuration.
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
    /// Gets the Vault path for write database credentials.
    /// </summary>
    public string VaultWritePath { get; internal set; } = default!;

    /// <summary>
    /// Gets the Vault path for read database credentials (if separate).
    /// </summary>
    public string? VaultReadPath { get; internal set; }

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
