using SharedKernel.Core.Events;

namespace SharedKernel.Events;

/// <summary>
/// Integration event raised when a new tenant has been created.
/// </summary>
public class TenantCreatedIntegrationEvent : IntegrationEvent
{
    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier (unique name/slug).
    /// </summary>
    public string Identifier { get; set; } = default!;

    /// <summary>
    /// Gets or sets the tenant display name.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Gets or sets the database strategy (Shared, Dedicated, External).
    /// </summary>
    public string DatabaseStrategy { get; set; } = default!;

    /// <summary>
    /// Gets or sets the database provider (PostgreSQL, SqlServer, MySQL).
    /// </summary>
    public string DatabaseProvider { get; set; } = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantCreatedIntegrationEvent"/> class.
    /// </summary>
    public TenantCreatedIntegrationEvent()
    {
        // Parameterless constructor for Wolverine/RabbitMQ serialization
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantCreatedIntegrationEvent"/> class.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="identifier">The tenant identifier (unique name/slug).</param>
    /// <param name="name">The tenant display name.</param>
    /// <param name="databaseStrategy">The database strategy.</param>
    /// <param name="databaseProvider">The database provider.</param>
    public TenantCreatedIntegrationEvent(
        Guid tenantId,
        string identifier,
        string name,
        string databaseStrategy,
        string databaseProvider)
    {
        TenantId = tenantId;
        Identifier = identifier;
        Name = name;
        DatabaseStrategy = databaseStrategy;
        DatabaseProvider = databaseProvider;
    }
}
