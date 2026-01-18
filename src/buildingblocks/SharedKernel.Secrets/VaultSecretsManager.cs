using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.AppRole;
using VaultSharp.V1.AuthMethods.Kubernetes;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;

namespace SharedKernel.Secrets;

/// <summary>
/// Implementation of secrets manager using HashiCorp Vault.
/// </summary>
public sealed class VaultSecretsManager : IVaultSecretsManager, IDisposable
{
    private readonly IVaultClient _vaultClient;
    private readonly VaultOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<VaultSecretsManager> _logger;
    private readonly TimeSpan _cacheDuration;

    public VaultSecretsManager(
        IOptions<VaultOptions> options,
        IMemoryCache cache,
        ILogger<VaultSecretsManager> logger)
    {
        _options = options.Value;
        _cache = cache;
        _logger = logger;
        _cacheDuration = TimeSpan.FromMinutes(_options.CacheDurationMinutes);

        var authMethod = CreateAuthMethod();
        var vaultClientSettings = new VaultClientSettings(
            _options.Address,
            authMethod)
        {
            VaultServiceTimeout = TimeSpan.FromSeconds(_options.TimeoutSeconds),
        };

        _vaultClient = new VaultClient(vaultClientSettings);
    }

    /// <inheritdoc />
    public async Task<DatabaseCredentials> GetDatabaseCredentialsAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"db-creds-{tenantId}";

        if (_cache.TryGetValue<DatabaseCredentials>(cacheKey, out var cachedCredentials)
            && cachedCredentials is not null)
        {
            _logger.LogDebug("Retrieved database credentials for tenant {TenantId} from cache", tenantId);
            return cachedCredentials;
        }

        var path = $"{_options.DatabaseSecretsPath}/{tenantId}";
        var credentials = await GetDatabaseCredentialsFromVaultAsync(path, cancellationToken);

        _cache.Set(cacheKey, credentials, _cacheDuration);
        _logger.LogInformation("Retrieved and cached database credentials for tenant {TenantId}", tenantId);

        return credentials;
    }

    /// <inheritdoc />
    public async Task<DatabaseCredentials> GetSharedDatabaseCredentialsAsync(
        CancellationToken cancellationToken = default)
    {
        const string cacheKey = "db-creds-shared";

        if (_cache.TryGetValue<DatabaseCredentials>(cacheKey, out var cachedCredentials)
            && cachedCredentials is not null)
        {
            _logger.LogDebug("Retrieved shared database credentials from cache");
            return cachedCredentials;
        }

        var path = $"{_options.DatabaseSecretsPath}/shared";
        var credentials = await GetDatabaseCredentialsFromVaultAsync(path, cancellationToken);

        _cache.Set(cacheKey, credentials, _cacheDuration);
        _logger.LogInformation("Retrieved and cached shared database credentials");

        return credentials;
    }

    /// <inheritdoc />
    public async Task StoreDatabaseCredentialsAsync(
        string tenantId,
        DatabaseCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        var path = $"{_options.DatabaseSecretsPath}/{tenantId}";
        var data = new Dictionary<string, string>
        {
            ["admin_username"] = credentials.Admin.Username,
            ["admin_password"] = credentials.Admin.Password,
            ["app_username"] = credentials.Application.Username,
            ["app_password"] = credentials.Application.Password,
            ["host"] = credentials.Host,
            ["port"] = credentials.Port.ToString(),
            ["database"] = credentials.Database,
        };

        if (credentials.AdditionalParameters is not null)
        {
            foreach (var (key, value) in credentials.AdditionalParameters)
            {
                data[$"param_{key}"] = value;
            }
        }

        await StoreSecretAsync(path, data, cancellationToken);

        // Invalidate cache
        var cacheKey = $"db-creds-{tenantId}";
        _cache.Remove(cacheKey);

        _logger.LogInformation("Stored database credentials for tenant {TenantId}", tenantId);
    }

    /// <inheritdoc />
    public async Task<string?> GetSecretAsync(
        string path,
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = $"{_options.MountPoint}/data/{path}";
            Secret<SecretData> secret = await _vaultClient.V1.Secrets.KeyValue.V2
                .ReadSecretAsync(path: path, mountPoint: _options.MountPoint);

            if (secret?.Data?.Data is null)
            {
                _logger.LogWarning("Secret not found at path {Path}", fullPath);
                return null;
            }

            if (!secret.Data.Data.TryGetValue(key, out var value))
            {
                _logger.LogWarning("Key {Key} not found in secret at path {Path}", key, fullPath);
                return null;
            }

            return value?.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret from path {Path}, key {Key}", path, key);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task StoreSecretAsync(
        string path,
        Dictionary<string, string> data,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _vaultClient.V1.Secrets.KeyValue.V2.WriteSecretAsync(
                path: path,
                data: data.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value),
                mountPoint: _options.MountPoint);

            _logger.LogInformation("Successfully stored secret at path {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store secret at path {Path}", path);
            throw;
        }
    }

    private async Task<DatabaseCredentials> GetDatabaseCredentialsFromVaultAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            Secret<SecretData> secret = await _vaultClient.V1.Secrets.KeyValue.V2
                .ReadSecretAsync(path: path, mountPoint: _options.MountPoint);

            if (secret?.Data?.Data is null)
            {
                throw new InvalidOperationException($"Database credentials not found at path {path}");
            }

            var data = secret.Data.Data;

            var adminUsername = GetRequiredValue(data, "admin_username", path);
            var adminPassword = GetRequiredValue(data, "admin_password", path);
            var appUsername = GetRequiredValue(data, "app_username", path);
            var appPassword = GetRequiredValue(data, "app_password", path);
            var host = GetRequiredValue(data, "host", path);
            var portStr = GetRequiredValue(data, "port", path);
            var database = GetRequiredValue(data, "database", path);

            if (!int.TryParse(portStr, out var port))
            {
                throw new InvalidOperationException($"Invalid port value '{portStr}' in credentials at {path}");
            }

            // Extract additional parameters
            var additionalParams = data
                .Where(kvp => kvp.Key.StartsWith("param_", StringComparison.Ordinal))
                .ToDictionary(
                    kvp => kvp.Key["param_".Length..],
                    kvp => kvp.Value?.ToString() ?? string.Empty);

            return new DatabaseCredentials
            {
                Admin = new UserCredentials
                {
                    Username = adminUsername,
                    Password = adminPassword,
                },
                Application = new UserCredentials
                {
                    Username = appUsername,
                    Password = appPassword,
                },
                Host = host,
                Port = port,
                Database = database,
                AdditionalParameters = additionalParams.Count > 0 ? additionalParams : null,
            };
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to retrieve database credentials from path {Path}", path);
            throw;
        }
    }

    private static string GetRequiredValue(
        IDictionary<string, object> data,
        string key,
        string path)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            throw new InvalidOperationException($"Required key '{key}' not found in credentials at {path}");
        }

        return value.ToString() ?? throw new InvalidOperationException(
            $"Value for key '{key}' is null in credentials at {path}");
    }

    private IAuthMethodInfo CreateAuthMethod()
    {
        return _options.AuthMethod switch
        {
            VaultAuthMethod.Token => new TokenAuthMethodInfo(_options.Token
                ?? throw new InvalidOperationException("Token is required for Token authentication")),

            VaultAuthMethod.AppRole => new AppRoleAuthMethodInfo(
                _options.RoleId ?? throw new InvalidOperationException("RoleId is required for AppRole authentication"),
                _options.SecretId ?? throw new InvalidOperationException("SecretId is required for AppRole authentication")),

            VaultAuthMethod.Kubernetes => new KubernetesAuthMethodInfo(
                _options.KubernetesRole ?? throw new InvalidOperationException("KubernetesRole is required for Kubernetes authentication"),
                File.ReadAllText(_options.KubernetesTokenPath ?? throw new InvalidOperationException("KubernetesTokenPath is required"))),

            _ => throw new NotSupportedException($"Authentication method {_options.AuthMethod} is not supported"),
        };
    }

    public void Dispose()
    {
        // VaultClient doesn't implement IDisposable, nothing to dispose
    }
}
