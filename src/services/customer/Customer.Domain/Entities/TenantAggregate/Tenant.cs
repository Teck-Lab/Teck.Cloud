using Customer.Domain.Entities.TenantAggregate.Events;
using ErrorOr;
using SharedKernel.Core.Domain;
using SharedKernel.Core.Pricing;

namespace Customer.Domain.Entities.TenantAggregate;

/// <summary>
/// Tenant aggregate root - represents a customer tenant in the system.
/// </summary>
public class Tenant : BaseEntity, IAggregateRoot
{
    /// <summary>
    /// Gets the tenant identifier (unique name/slug for resolution).
    /// </summary>
    public string Identifier { get; private set; } = default!;

    /// <summary>
    /// Gets the tenant display name.
    /// </summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Gets the tenant plan (e.g., "Free", "Pro", "Enterprise").
    /// </summary>
    public string Plan { get; private set; } = default!;

    /// <summary>
    /// Gets the database strategy for this tenant.
    /// </summary>
    public DatabaseStrategy DatabaseStrategy { get; private set; } = default!;

    /// <summary>
    /// Gets the database provider for this tenant.
    /// </summary>
    public DatabaseProvider DatabaseProvider { get; private set; } = default!;

    /// <summary>
    /// Gets a value indicating whether the tenant is active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets the database metadata for each service.
    /// </summary>
    private readonly List<TenantDatabaseMetadata> _databases = new();

    /// <summary>
    /// Gets the database metadata for each service.
    /// </summary>
    public IReadOnlyList<TenantDatabaseMetadata> Databases => _databases.AsReadOnly();

    private Tenant() { } // EF Core constructor

    /// <summary>
    /// Creates a new tenant.
    /// </summary>
    /// <param name="identifier">The tenant identifier.</param>
    /// <param name="name">The tenant name.</param>
    /// <param name="plan">The tenant plan.</param>
    /// <param name="databaseStrategy">The database strategy.</param>
    /// <param name="databaseProvider">The database provider.</param>
    /// <returns>The created tenant or validation errors.</returns>
    public static ErrorOr<Tenant> Create(
        string identifier,
        string name,
        string plan,
        DatabaseStrategy databaseStrategy,
        DatabaseProvider databaseProvider)
    {
        var errors = new List<Error>();

        if (string.IsNullOrWhiteSpace(identifier))
            errors.Add(Error.Validation("Tenant.Identifier", "Identifier cannot be empty"));

        if (string.IsNullOrWhiteSpace(name))
            errors.Add(Error.Validation("Tenant.Name", "Name cannot be empty"));

        if (string.IsNullOrWhiteSpace(plan))
            errors.Add(Error.Validation("Tenant.Plan", "Plan cannot be empty"));

        if (errors.Count > 0)
            return errors;

        var tenant = new Tenant
        {
            Identifier = identifier,
            Name = name,
            Plan = plan,
            DatabaseStrategy = databaseStrategy,
            DatabaseProvider = databaseProvider,
            IsActive = true,
        };

        tenant.AddDomainEvent(new TenantCreatedDomainEvent(
            tenant.Id,
            identifier,
            name,
            databaseStrategy.Name,
            databaseProvider.Name));

        return tenant;
    }

    /// <summary>
    /// Adds database metadata for a service.
    /// </summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="writeEnvVarKey">The environment variable key for the write DSN.</param>
    /// <param name="readEnvVarKey">The environment variable key for the read DSN, if separate.</param>
    /// <param name="hasSeparateReadDatabase">Whether the tenant has a separate read database.</param>
    public void AddDatabaseMetadata(
        string serviceName,
        string writeEnvVarKey,
        string? readEnvVarKey,
        bool hasSeparateReadDatabase)
    {
        var metadata = new TenantDatabaseMetadata
        {
            TenantId = Id,
            ServiceName = serviceName,
            WriteEnvVarKey = writeEnvVarKey,
            ReadEnvVarKey = readEnvVarKey,
            HasSeparateReadDatabase = hasSeparateReadDatabase,
        };

        _databases.Add(metadata);
    }

    /// <summary>
    /// Deactivates the tenant.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Activates the tenant.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }
}
