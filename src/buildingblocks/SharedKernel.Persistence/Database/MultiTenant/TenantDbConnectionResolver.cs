using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.Core.Pricing;
using SharedKernel.Infrastructure.MultiTenant;
using ZiggyCreatures.Caching.Fusion;

namespace SharedKernel.Persistence.Database.MultiTenant
{
    /// <summary>
    /// Default implementation of the tenant database connection resolver.
    /// </summary>
    public class TenantDbConnectionResolver : ITenantDbConnectionResolver
    {
        // Cache for tenant connection information as a fallback/performance optimization
        private static readonly ConcurrentDictionary<string, (string WriteConnectionString, string? ReadConnectionString, DatabaseProvider Provider, DatabaseStrategy Strategy)> _connectionCache =
            new ConcurrentDictionary<string, (string, string?, DatabaseProvider, DatabaseStrategy)>();
        private readonly string _defaultWriteConnectionString;
        private readonly string _defaultReadConnectionString;
        private readonly DatabaseProvider _defaultProvider;
        private readonly ILogger<TenantDbConnectionResolver> _logger;
        private readonly Lazy<IFusionCache> _fusionCache;
        private readonly Lazy<IHttpClientFactory> _httpClientFactory;

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

            _fusionCache = new Lazy<IFusionCache>(() =>
                serviceProvider.GetService<IFusionCache>() ??
                throw new InvalidOperationException("FusionCache service is not registered"));

            _httpClientFactory = new Lazy<IHttpClientFactory>(() =>
                serviceProvider.GetService<IHttpClientFactory>() ??
                throw new InvalidOperationException("HttpClientFactory service is not registered"));
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

            // Try to get from static cache first for best performance
            if (!string.IsNullOrEmpty(tenantInfo.Id) && _connectionCache.TryGetValue(tenantInfo.Id, out var cachedConnection))
            {
                return cachedConnection;
            }

            // Use FusionCache with a factory that will fetch from Customer.Api if needed
            // This allows for distributed caching with automatic background refresh
            try
            {
                var cacheKey = $"tenant-db-connection:{tenantInfo.Id}";
                var fusionCache = _fusionCache.Value;
                var tenantDbInfo = fusionCache.GetOrSet(
                    cacheKey,
                    _ => FetchTenantDatabaseInfoAsync(tenantInfo.Id ?? string.Empty),
                    options => options
                        .SetDuration(TimeSpan.FromMinutes(30))
                        .SetFailSafe(true)
                        .SetFactoryTimeouts(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(2))
                ).Result;

                if (tenantDbInfo != null)
                {
                    string writeConnectionString = tenantDbInfo.WriteConnectionString ?? _defaultWriteConnectionString;
                    string? readConnectionString;
                    DatabaseProvider.TryFromName(tenantDbInfo.Provider, out var provider);
                    DatabaseStrategy.TryFromName(tenantDbInfo.Strategy, out var strategy);

                    if (tenantDbInfo.HasReadReplicas)
                    {
                        readConnectionString = tenantDbInfo.ReadConnectionString;
                        if (string.IsNullOrWhiteSpace(readConnectionString))
                        {
                            // If HasReadReplicas is true but no read connection string, use write connection string
                            readConnectionString = writeConnectionString;
                        }
                    }
                    else
                    {
                        // If HasReadReplicas is false, use write connection string for reads
                        readConnectionString = writeConnectionString;
                    }

                    var result = (writeConnectionString, readConnectionString, provider, strategy);

                    // Cache the result for future use
                    if (!string.IsNullOrEmpty(tenantInfo.Id))
                    {
                        _connectionCache.TryAdd(tenantInfo.Id, result);
                    }

                    // Ensure write connection string is not null or empty before returning
                    if (!string.IsNullOrEmpty(result.writeConnectionString))
                    {
                        return result;
                    }
                    else
                    {
                        // Fallback to default if result is invalid
                        return (_defaultWriteConnectionString, _defaultReadConnectionString, _defaultProvider, DatabaseStrategy.Shared);
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error resolving tenant connection info from cache/API for tenant {TenantId}", tenantInfo.Id);

                // Continue to fallback method
            }

            // Fallback to looking in tenant properties if API fetch fails
            return GetConnectionFromTenantProperties(tenantInfo);
        }

        /// <summary>
        /// Safely resolves the connection string, database provider, and strategy for a tenant.
        /// This method provides additional safety checks for premium/enterprise tenants.
        /// </summary>
        /// <param name="tenantInfo">The tenant information.</param>
        /// <param name="requireCustomerApi">Whether to require the Customer API for non-shared tenants.</param>
        /// <returns>A task containing the tenant connection result.</returns>
        public async Task<TenantConnectionResult> ResolveTenantConnectionSafelyAsync(TenantDetails tenantInfo, bool requireCustomerApi = true)
        {
            // If tenant is null, use default shared connection
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

            // Try to get from static cache first
            if (!string.IsNullOrEmpty(tenantInfo.Id) && _connectionCache.TryGetValue(tenantInfo.Id, out var cachedConnection))
            {
                var (writeConnectionString, readConnectionString, provider, strategy) = cachedConnection;

                // For shared tenants, cached connection is always safe
                if (strategy == DatabaseStrategy.Shared)
                {
                    return TenantConnectionResult.Success(writeConnectionString, readConnectionString, provider, strategy, customerApiAvailable: true, fromCache: true);
                }

                // For premium/enterprise tenants, check if we require Customer API
                if (requireCustomerApi && (strategy == DatabaseStrategy.Dedicated ||
                                          strategy == DatabaseStrategy.External))
                {
                    // Try to fetch fresh data from Customer API
                    var apiResult = await TryFetchTenantDatabaseInfoAsync(tenantInfo.Id);
                    if (apiResult.Success)
                    {
                        // Update cache with fresh data
                        var freshWriteConnection = apiResult.Data.WriteConnectionString ?? writeConnectionString;
                        var freshReadConnection = apiResult.Data.ReadConnectionString;
                        if (string.IsNullOrWhiteSpace(freshReadConnection) || freshReadConnection == freshWriteConnection)
                        {
                            freshReadConnection = null;
                        }

                        DatabaseProvider.TryFromName(apiResult.Data.Provider, out var providerResult);
                        DatabaseStrategy.TryFromName(apiResult.Data.Strategy, out var strategyResult);
                        var freshConnection = (freshWriteConnection, freshReadConnection, providerResult, strategyResult);
                        if (!string.IsNullOrEmpty(tenantInfo.Id))
                        {
                            _connectionCache.TryAdd(tenantInfo.Id, freshConnection);
                        }

                        return TenantConnectionResult.Success(
                            freshConnection.freshWriteConnection,
                            freshConnection.freshReadConnection,
                            freshConnection.providerResult,
                            freshConnection.strategyResult,
                            customerApiAvailable: true,
                            fromCache: false);
                    }
                    else
                    {
                        // Customer API is unavailable for premium/enterprise tenant
                        return TenantConnectionResult.UnsafeForMigration(
                            writeConnectionString,
                            provider,
                            strategy,
                            $"Customer API is unavailable for {strategy} tenant {tenantInfo.Id}. Using cached connection is unsafe for migration.");
                    }
                }

                // If we don't require Customer API or it's a shared tenant, use cached connection
                return TenantConnectionResult.Success(writeConnectionString, readConnectionString, provider, strategy, customerApiAvailable: false, fromCache: true);
            }

            // No cache hit, try to fetch from Customer API
            var fetchResult = await TryFetchTenantDatabaseInfoAsync(tenantInfo.Id ?? string.Empty);
            if (fetchResult.Success)
            {
                var tenantDbInfo = fetchResult.Data;
                string writeConnectionString = tenantDbInfo.WriteConnectionString ?? _defaultWriteConnectionString;
                string? readConnectionString = tenantDbInfo.ReadConnectionString;
                if (string.IsNullOrWhiteSpace(readConnectionString) || readConnectionString == writeConnectionString)
                {
                    readConnectionString = null;
                }

                DatabaseProvider.TryFromName(tenantDbInfo.Provider, out var provider);
                DatabaseStrategy.TryFromName(tenantDbInfo.Strategy, out var strategy);

                var result = (writeConnectionString, readConnectionString, provider, strategy);

                // Cache the result for future use
                if (!string.IsNullOrEmpty(tenantInfo.Id))
                {
                    _connectionCache.TryAdd(tenantInfo.Id, result);
                }

                return TenantConnectionResult.Success(writeConnectionString, readConnectionString, provider, strategy, customerApiAvailable: true, fromCache: false);
            }

            // Customer API is unavailable and no cache - fallback to tenant properties
            var fallbackResult = GetConnectionFromTenantProperties(tenantInfo);

            // For premium/enterprise tenants, mark as unsafe if Customer API is required
            if (requireCustomerApi && (fallbackResult.Strategy == DatabaseStrategy.Dedicated ||
                                       fallbackResult.Strategy == DatabaseStrategy.External))
            {
                return TenantConnectionResult.UnsafeForMigration(
                    fallbackResult.WriteConnectionString,
                    fallbackResult.Provider,
                    fallbackResult.Strategy,
                    $"Customer API is unavailable for {fallbackResult.Strategy} tenant {tenantInfo.Id}. Using fallback connection is unsafe for migration.");
            }

            // For shared tenants or when Customer API is not required, return success
            return TenantConnectionResult.Success(
                fallbackResult.WriteConnectionString,
                fallbackResult.ReadConnectionString,
                fallbackResult.Provider,
                fallbackResult.Strategy,
                customerApiAvailable: false,
                fromCache: false);
        }

        /// <summary>
        /// Attempts to fetch tenant database information from the Customer API with proper error handling.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <returns>A result indicating success or failure with the tenant database information.</returns>
        private async Task<(bool Success, TenantDatabaseInfo Data)> TryFetchTenantDatabaseInfoAsync(string tenantId)
        {
            try
            {
                var httpClient = _httpClientFactory.Value.CreateClient("CustomerApi");
                var requestUri = new Uri($"api/v1/tenants/{tenantId}/database-info", UriKind.Relative);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await httpClient.GetAsync(requestUri, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<TenantDatabaseInfo>(cancellationToken: cts.Token);
                    _logger.LogInformation("Successfully fetched database info for tenant {TenantId}", tenantId);
                    return (true, result ?? new TenantDatabaseInfo());
                }

                _logger.LogWarning(
                    "Failed to fetch tenant database info for tenant {TenantId}. Status code: {StatusCode}",
                    tenantId,
                    response.StatusCode);
                return (false, new TenantDatabaseInfo());
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error fetching tenant database info for tenant {TenantId}", tenantId);
                return (false, new TenantDatabaseInfo());
            }
        }

        /// <summary>
        /// Fetches tenant database information from the Customer.Api service.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <returns>The tenant database information or null if not found or an error occurs.</returns>
        private async Task<TenantDatabaseInfo?> FetchTenantDatabaseInfoAsync(string tenantId)
        {
            try
            {
                var httpClient = _httpClientFactory.Value.CreateClient("CustomerApi");
                var requestUri = new Uri($"api/v1/tenants/{tenantId}/database-info", UriKind.Relative);
                var response = await httpClient.GetAsync(requestUri);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<TenantDatabaseInfo>();
                    _logger.LogInformation("Successfully fetched database info for tenant {TenantId}", tenantId);
                    return result;
                }

                _logger.LogWarning(
                    "Failed to fetch tenant database info for tenant {TenantId}. Status code: {StatusCode}",
                    tenantId,
                    response.StatusCode);
                return null;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error fetching tenant database info for tenant {TenantId}", tenantId);
                return null;
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
            DatabaseStrategy.TryFromName(tenantDetails.DatabaseStrategy, out var strategy);

            // Use TenantDetails properties directly
            string writeConnectionString = tenantDetails.WriteConnectionString ?? _defaultWriteConnectionString;
            string? readConnectionString;

            if (tenantDetails.HasReadReplicas)
            {
                readConnectionString = tenantDetails.ReadConnectionString;
                if (string.IsNullOrWhiteSpace(readConnectionString))
                {
                    // If HasReadReplicas is true but no read connection string, use write connection string
                    readConnectionString = writeConnectionString;
                }
            }
            else
            {
                // If HasReadReplicas is false, use write connection string for reads
                readConnectionString = writeConnectionString;
            }

            // Determine database provider (default to PostgreSQL)
            DatabaseProvider.TryFromName(tenantDetails.DatabaseProvider, out var provider);

            // Cache the result
            var fullResult = (writeConnectionString, readConnectionString, provider, strategy);
            if (!string.IsNullOrEmpty(tenantDetails.Id))
            {
                _connectionCache.TryAdd(tenantDetails.Id, fullResult);
            }

            return fullResult;
        }

        /// <summary>
        /// DTO for tenant database information.
        /// </summary>
        public class TenantDatabaseInfo
        {
            /// <summary>
            /// Gets or sets the tenant identifier.
            /// </summary>
            [JsonPropertyName("tenantId")]
            public string TenantId { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the write connection string.
            /// </summary>
            [JsonPropertyName("writeConnectionString")]
            public string? WriteConnectionString { get; set; }

            /// <summary>
            /// Gets or sets the read connection string.
            /// </summary>
            [JsonPropertyName("readConnectionString")]
            public string? ReadConnectionString { get; set; }

            /// <summary>
            /// Gets or sets the database provider (PostgreSQL, SqlServer, MySQL).
            /// </summary>
            [JsonPropertyName("provider")]
            public string Provider { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the database strategy (Shared, Dedicated, External).
            /// </summary>
            [JsonPropertyName("strategy")]
            public string Strategy { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets a value indicating whether indicates whether the tenant's database is configured with read replicas for improved read performance.
            /// </summary>
            [JsonPropertyName("hasReadReplicas")]
            public bool HasReadReplicas { get; set; }
        }
    }
}
