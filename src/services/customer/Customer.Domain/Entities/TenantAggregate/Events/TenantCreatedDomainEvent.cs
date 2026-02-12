using SharedKernel.Core.Events;

namespace Customer.Domain.Entities.TenantAggregate.Events;

/// <summary>
/// Domain event raised when a tenant is created.
/// </summary>
public sealed class TenantCreatedDomainEvent : DomainEvent
{
    /// <summary>
    /// Gets the tenant ID.
    /// </summary>
    public Guid TenantId { get; }

    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public string Identifier { get; }

    /// <summary>
    /// Gets the tenant name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the database strategy.
    /// </summary>
    public string DatabaseStrategy { get; }

    /// <summary>
    /// Gets the database provider.
    /// </summary>
    public string DatabaseProvider { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantCreatedDomainEvent"/> class.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="identifier">The tenant identifier.</param>
    /// <param name="name">The tenant name.</param>
    /// <param name="databaseStrategy">The database strategy.</param>
    /// <param name="databaseProvider">The database provider.</param>
    public TenantCreatedDomainEvent(
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
