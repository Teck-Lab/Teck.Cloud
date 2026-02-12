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
    /// Gets the environment variable key for write database connection string.
    /// Example: ConnectionStrings__Tenants__{tenantId}__Write.
    /// </summary>
    public string WriteEnvVarKey { get; init; } = default!;

    /// <summary>
    /// Gets the environment variable key for read database connection string.
    /// </summary>
    public string? ReadEnvVarKey { get; init; }

    /// <summary>
    /// Gets a value indicating whether this service has a separate read database.
    /// </summary>
    public bool HasSeparateReadDatabase { get; init; }
}

