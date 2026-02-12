namespace SharedKernel.Secrets;

/// <summary>
/// Service for managing secrets stored in HashiCorp Vault.
/// </summary>
public interface IVaultSecretsManager
{
    /// <summary>
    /// Retrieves database credentials for a tenant (legacy method - uses convention-based path).
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Database credentials with admin and application users.</returns>
    Task<DatabaseCredentials> GetDatabaseCredentialsAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves database credentials from a specific Vault path.
    /// </summary>
    /// <param name="vaultPath">Full Vault path (e.g., "database/shared/postgres/catalog/write").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Database credentials with admin and application users.</returns>
    Task<DatabaseCredentials> GetDatabaseCredentialsByPathAsync(
        string vaultPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves database credentials for the shared database (legacy method - uses convention-based path).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Database credentials with admin and application users.</returns>
    Task<DatabaseCredentials> GetSharedDatabaseCredentialsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves database credentials for a shared database with service-aware path.
    /// </summary>
    /// <param name="serviceName">The service name (e.g., "catalog", "orders").</param>
    /// <param name="provider">Database provider (e.g., "postgres", "sqlserver").</param>
    /// <param name="isReadDatabase">Whether this is for a read database.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Database credentials with admin and application users.</returns>
    Task<DatabaseCredentials> GetSharedDatabaseCredentialsAsync(
        string serviceName,
        string provider,
        bool isReadDatabase = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores database credentials for a tenant (legacy method - uses convention-based path).
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="credentials">Database credentials to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StoreDatabaseCredentialsAsync(
        string tenantId,
        DatabaseCredentials credentials,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores database credentials at a specific Vault path.
    /// </summary>
    /// <param name="vaultPath">Full Vault path (e.g., "database/tenants/tenant-123/catalog/write").</param>
    /// <param name="credentials">Database credentials to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StoreDatabaseCredentialsByPathAsync(
        string vaultPath,
        DatabaseCredentials credentials,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if database credentials exist at a specific Vault path.
    /// </summary>
    /// <param name="vaultPath">Full Vault path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if credentials exist, false otherwise.</returns>
    Task<bool> CredentialsExistAsync(
        string vaultPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a secret value by path.
    /// </summary>
    /// <param name="path">Path to the secret in Vault.</param>
    /// <param name="key">Key within the secret.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Secret value.</returns>
    Task<string?> GetSecretAsync(
        string path,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a secret value.
    /// </summary>
    /// <param name="path">Path to the secret in Vault.</param>
    /// <param name="data">Secret data to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StoreSecretAsync(
        string path,
        Dictionary<string, string> data,
        CancellationToken cancellationToken = default);
}
