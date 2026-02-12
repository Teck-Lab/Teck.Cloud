namespace Customer.Application.Tenants.DTOs;

/// <summary>
/// Data transfer object for service database information.
/// </summary>
public record ServiceDatabaseInfoDto
{
    /// <summary>
    /// Gets the Vault path for write database credentials.
    /// </summary>
    public string VaultWritePath { get; init; } = default!;

    /// <summary>
    /// Gets the Vault path for read database credentials.
    /// </summary>
    public string? VaultReadPath { get; init; }

    /// <summary>
    /// Gets a value indicating whether this service has a separate read database.
    /// </summary>
    public bool HasSeparateReadDatabase { get; init; }
}
