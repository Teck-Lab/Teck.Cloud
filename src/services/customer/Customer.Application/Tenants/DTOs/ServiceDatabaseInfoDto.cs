namespace Customer.Application.Tenants.DTOs;

/// <summary>
/// Data transfer object for service database information.
/// </summary>
public record ServiceDatabaseInfoDto
{
    /// <summary>
    /// Gets the environment variable key for write database connection string.
    /// </summary>
    public string WriteEnvVarKey { get; init; } = default!;

    /// <summary>
    /// Gets the environment variable key for read database connection string (if separate).
    /// </summary>
    public string? ReadEnvVarKey { get; init; }

    /// <summary>
    /// Gets a value indicating whether this service has a separate read database.
    /// </summary>
    public bool HasSeparateReadDatabase { get; init; }
}
