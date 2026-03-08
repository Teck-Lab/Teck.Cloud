using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.Core.Pricing;
using SharedKernel.Infrastructure.MultiTenant;

namespace SharedKernel.Persistence.Database.MultiTenant
{
    /// <summary>
    /// Default implementation of the tenant database connection resolver.
    /// </summary>
    public class TenantDbConnectionResolver : ITenantDbConnectionResolver
    {
        private const string TenantIdHeader = "X-TenantId";
        private const string TenantDbStrategyHeader = "X-Tenant-DbStrategy";

        // Cache for tenant connection information as a fallback/performance optimization
        private static readonly ConcurrentDictionary<string, (string WriteConnectionString, string? ReadConnectionString, DatabaseProvider Provider, DatabaseStrategy Strategy)> _connectionCache =
            new ConcurrentDictionary<string, (string, string?, DatabaseProvider, DatabaseStrategy)>();
        private readonly string _defaultWriteConnectionString;
        private readonly string _defaultReadConnectionString;
        private readonly DatabaseProvider _defaultProvider;
        private readonly ILogger<TenantDbConnectionResolver> _logger;
        private readonly Lazy<IConfiguration> _configuration;
        private readonly Lazy<IHttpContextAccessor?> _httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="TenantDbConnectionResolver"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider for dependency injection.</param>
        /// <param name="defaultWriteConnectionString">The default write connection string for shared database.</param>
        /// <param name="defaultReadConnectionString">The default read connection string for shared database.</param>
        /// <param name="defaultProvider">The default database provider.</param>
        public TenantDbConnectionResolver(
            IServiceProvider serviceProvider,
            string defaultWriteConnectionString,
            string defaultReadConnectionString,
            DatabaseProvider defaultProvider)
        {
            _defaultWriteConnectionString = defaultWriteConnectionString;
            _defaultReadConnectionString = defaultReadConnectionString;
            _defaultProvider = defaultProvider;

            // Use lazy initialization to avoid circular dependencies
            _logger = serviceProvider.GetService<ILogger<TenantDbConnectionResolver>>() ??
                throw new InvalidOperationException("Logger service is not registered");

            _configuration = new Lazy<IConfiguration>(() =>
                serviceProvider.GetService<IConfiguration>() ??
                throw new InvalidOperationException("Configuration service is not registered"));

            _httpContextAccessor = new Lazy<IHttpContextAccessor?>(() =>
                serviceProvider.GetService<IHttpContextAccessor>());
        }

        /// <summary>
        /// Resolves the connection string, database provider, and strategy for a tenant.
        /// </summary>
        /// <param name="tenantInfo">The tenant information.</param>
        /// <returns>A tuple containing the connection string, database provider, and strategy.</returns>
        public (string WriteConnectionString, string? ReadConnectionString, DatabaseProvider Provider, DatabaseStrategy Strategy) ResolveTenantConnection(TenantDetails tenantInfo)
        {
            // If tenant is null, use default shared connection
            if (tenantInfo == null)
            {
                return (_defaultWriteConnectionString, _defaultReadConnectionString, _defaultProvider, DatabaseStrategy.Shared);
            }

            if (TryResolveFromGatewayHint(tenantInfo, out var hintedConnection))
            {
                return hintedConnection;
            }

            // Try to get from static cache first for best performance
            if (!string.IsNullOrEmpty(tenantInfo.Id) && _connectionCache.TryGetValue(tenantInfo.Id, out var cachedConnection))
            {
                return cachedConnection;
            }

            return GetConnectionFromTenantProperties(tenantInfo);
        }

        private bool TryResolveFromGatewayHint(
            TenantDetails tenantInfo,
            out (string WriteConnectionString, string? ReadConnectionString, DatabaseProvider Provider, DatabaseStrategy Strategy) connection)
        {
            connection = default;

            var headers = _httpContextAccessor.Value?.HttpContext?.Request?.Headers;
            if (headers == null)
            {
                return false;
            }

            if (headers.TryGetValue(TenantIdHeader, out var tenantHeader) &&
                !string.IsNullOrWhiteSpace(tenantInfo.Id) &&
                !string.Equals(tenantHeader.ToString(), tenantInfo.Id, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!headers.TryGetValue(TenantDbStrategyHeader, out var strategyHeader) ||
                string.IsNullOrWhiteSpace(strategyHeader))
            {
                return false;
            }

            if (!DatabaseStrategy.TryFromName(strategyHeader.ToString(), true, out var strategy))
            {
                return false;
            }

            if (strategy == DatabaseStrategy.Shared)
            {
                connection = GetSharedConnection(tenantInfo);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(tenantInfo.Id) &&
                _connectionCache.TryGetValue(tenantInfo.Id, out var cachedConnection) &&
                cachedConnection.Strategy == strategy)
            {
                connection = cachedConnection;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Safely resolves the connection string, database provider, and strategy for a tenant.
        /// </summary>
        /// <param name="tenantInfo">The tenant information.</param>
        /// <param name="requireCustomerApi">Unused compatibility parameter.</param>
        /// <returns>A task containing the tenant connection result.</returns>
        public async Task<TenantConnectionResult> ResolveTenantConnectionSafelyAsync(TenantDetails tenantInfo, bool requireCustomerApi = true)
        {
            if (tenantInfo == null)
            {
                return TenantConnectionResult.Success(
                    _defaultWriteConnectionString,
                    _defaultReadConnectionString,
                    _defaultProvider,
                    DatabaseStrategy.Shared,
                    customerApiAvailable: true,
                    fromCache: false);
            }

            if (!string.IsNullOrEmpty(tenantInfo.Id) && _connectionCache.TryGetValue(tenantInfo.Id, out var cachedConnection))
            {
                var (writeConnectionString, readConnectionString, provider, strategy) = cachedConnection;
                return TenantConnectionResult.Success(writeConnectionString, readConnectionString, provider, strategy, customerApiAvailable: false, fromCache: true);
            }

            try
            {
                var resolved = await Task.Run(() => GetConnectionFromTenantProperties(tenantInfo)).ConfigureAwait(false);
                return TenantConnectionResult.Success(
                    resolved.WriteConnectionString,
                    resolved.ReadConnectionString,
                    resolved.Provider,
                    resolved.Strategy,
                    customerApiAvailable: false,
                    fromCache: false);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error resolving local tenant database info for tenant {TenantId}", tenantInfo.Id);
                return TenantConnectionResult.Failure($"Failed to resolve tenant connection for '{tenantInfo.Id}': {exception.Message}");
            }
        }

        /// <summary>
        /// Gets tenant connection info from properties stored in the tenant object.
        /// This serves as a fallback if the API call fails.
        /// </summary>
        private (string WriteConnectionString, string? ReadConnectionString, DatabaseProvider Provider, DatabaseStrategy Strategy)
            GetConnectionFromTenantProperties(TenantDetails tenantDetails)
        {
            // Determine tenant database strategy
            bool hasValidStrategy = DatabaseStrategy.TryFromName(tenantDetails.DatabaseStrategy, true, out var strategy);
            strategy ??= DatabaseStrategy.Shared;

            if (strategy == DatabaseStrategy.Shared)
            {
                return GetSharedConnection(tenantDetails);
            }

            string writeConnectionString = ResolveLocalTenantConnectionString(tenantDetails, readOnly: false)
                ?? tenantDetails.WriteConnectionString
                ?? _defaultWriteConnectionString;

            if (string.IsNullOrWhiteSpace(writeConnectionString))
            {
                throw new InvalidOperationException($"Missing write connection string for tenant '{tenantDetails.Id}'.");
            }

            bool hasValidProvider = DatabaseProvider.TryFromName(tenantDetails.DatabaseProvider, true, out var provider);
            provider ??= _defaultProvider;

            if (!hasValidStrategy)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Invalid or missing tenant database strategy '{DatabaseStrategy}' for tenant {TenantId}. Falling back to strategy '{FallbackStrategy}'.",
                        tenantDetails.DatabaseStrategy,
                        tenantDetails.Id,
                        strategy.Name);
                }
            }

            if (!hasValidProvider)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Invalid or missing tenant database provider '{DatabaseProvider}' for tenant {TenantId}. Falling back to provider '{FallbackProvider}'.",
                        tenantDetails.DatabaseProvider,
                        tenantDetails.Id,
                        provider.Name);
                }
            }

            string? localReadConnection = ResolveLocalTenantConnectionString(tenantDetails, readOnly: true);
            string? readConnectionString = ResolveReadConnectionString(
                writeConnectionString,
                localReadConnection ?? tenantDetails.ReadConnectionString,
                tenantDetails.HasReadReplicas,
                provider,
                tenantDetails.DatabaseProvider);

            // Cache the result
            var fullResult = (writeConnectionString, readConnectionString, provider, strategy);
            if (!string.IsNullOrEmpty(tenantDetails.Id))
            {
                _connectionCache.TryAdd(tenantDetails.Id, fullResult);
            }

            return fullResult;
        }

        private string? ResolveLocalTenantConnectionString(TenantDetails tenantDetails, bool readOnly)
        {
            string side = readOnly ? "Read" : "Write";
            var candidates = new[]
            {
                tenantDetails.Id,
                tenantDetails.Identifier,
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(value => $"ConnectionStrings__Tenants__{value}__{side}")
            .ToArray();

            foreach (var envStyleKey in candidates)
            {
                string? fromFile = TryReadSecretFile(envStyleKey);
                if (!string.IsNullOrWhiteSpace(fromFile))
                {
                    return fromFile;
                }
            }

            IConfiguration configuration = _configuration.Value;
            foreach (var envStyleKey in candidates)
            {
                string configKey = envStyleKey.Replace("__", ":", StringComparison.Ordinal);
                string? fromConfiguration = configuration[configKey];
                if (!string.IsNullOrWhiteSpace(fromConfiguration))
                {
                    return fromConfiguration;
                }

                string? fromEnvironment = Environment.GetEnvironmentVariable(envStyleKey);
                if (!string.IsNullOrWhiteSpace(fromEnvironment))
                {
                    return fromEnvironment;
                }
            }

            return null;
        }

        private string? TryReadSecretFile(string envStyleKey)
        {
            IConfiguration configuration = _configuration.Value;
            string? configuredDirectory =
                configuration["TenantConnectionSecrets:Directory"]
                ?? Environment.GetEnvironmentVariable("TENANT_CONNECTION_SECRETS_DIRECTORY");

            var directories = new List<string>();
            if (!string.IsNullOrWhiteSpace(configuredDirectory))
            {
                directories.Add(configuredDirectory);
            }

            directories.Add("/run/secrets");
            directories.Add("/mnt/secrets-store");
            directories.Add("/var/run/secrets");

            foreach (string directory in directories
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(Directory.Exists))
            {
                try
                {
                    var fileCandidates = new[]
                    {
                        Path.Combine(directory, envStyleKey),
                        Path.Combine(directory, envStyleKey.Replace("__", ":", StringComparison.Ordinal)),
                        Path.Combine(directory, envStyleKey.Replace("__", "-", StringComparison.Ordinal)),
                    };

                    foreach (string filePath in fileCandidates.Where(File.Exists))
                    {
                        string value = File.ReadAllText(filePath).Trim();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            return value;
                        }
                    }
                }
                catch (Exception exception)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(exception, "Failed reading tenant connection secret file in directory {Directory}", directory);
                    }
                }
            }

            return null;
        }

        private static string? ResolveReadConnectionString(
            string writeConnectionString,
            string? candidateReadConnectionString,
            bool hasReadReplicas,
            DatabaseProvider provider,
            string? providerName)
        {
            if (!hasReadReplicas)
            {
                return writeConnectionString;
            }

            if (!string.IsNullOrWhiteSpace(candidateReadConnectionString))
            {
                return candidateReadConnectionString;
            }

            if (IsMySqlFamily(provider, providerName))
            {
                throw new InvalidOperationException(
                    "Read connection string is required for MySQL/MariaDB tenants configured with separate read mode.");
            }

            return writeConnectionString;
        }

        private static bool IsMySqlFamily(DatabaseProvider provider, string? providerName)
        {
            if (provider == DatabaseProvider.MySQL)
            {
                return true;
            }

            return providerName?.Contains("mysql", StringComparison.OrdinalIgnoreCase) == true
                || providerName?.Contains("mariadb", StringComparison.OrdinalIgnoreCase) == true;
        }

        private (string WriteConnectionString, string? ReadConnectionString, DatabaseProvider Provider, DatabaseStrategy Strategy)
            GetSharedConnection(TenantDetails tenantDetails)
        {
            bool hasValidSharedProvider = DatabaseProvider.TryFromName(tenantDetails.DatabaseProvider, true, out var sharedProvider);
            sharedProvider ??= _defaultProvider;

            if (!hasValidSharedProvider)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Invalid or missing shared tenant database provider '{DatabaseProvider}' for tenant {TenantId}. Falling back to provider '{FallbackProvider}'.",
                        tenantDetails.DatabaseProvider,
                        tenantDetails.Id,
                        sharedProvider.Name);
                }
            }

            string writeConnectionString = _defaultWriteConnectionString;
            string? localReadConnection = ResolveLocalTenantConnectionString(tenantDetails, readOnly: true);
            string? candidateReadConnection =
                localReadConnection
                ?? tenantDetails.ReadConnectionString
                ?? _defaultReadConnectionString;
            string? readConnectionString = tenantDetails.HasReadReplicas
                ? ResolveReadConnectionString(
                    writeConnectionString,
                    candidateReadConnection,
                    hasReadReplicas: true,
                    sharedProvider,
                    tenantDetails.DatabaseProvider)
                : _defaultReadConnectionString;

            var sharedResult = (writeConnectionString, readConnectionString, sharedProvider, DatabaseStrategy.Shared);
            if (!string.IsNullOrEmpty(tenantDetails.Id))
            {
                _connectionCache[tenantDetails.Id] = sharedResult;
            }

            return sharedResult;
        }
    }
}
