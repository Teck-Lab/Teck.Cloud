namespace SharedKernel.Secrets;

/// <summary>
/// Service for managing secrets stored in HashiCorp Vault.
/// </summary>
public interface IVaultSecretsManager
{
    /// <summary>
    /// Retrieves database credentials for a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Database credentials with admin and application users.</returns>
    Task<DatabaseCredentials> GetDatabaseCredentialsAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves database credentials for the shared database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Database credentials with admin and application users.</returns>
    Task<DatabaseCredentials> GetSharedDatabaseCredentialsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores database credentials for a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="credentials">Database credentials to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StoreDatabaseCredentialsAsync(
        string tenantId,
        DatabaseCredentials credentials,
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
