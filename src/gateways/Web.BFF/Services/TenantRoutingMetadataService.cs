using System.Text.Json.Serialization;
using ZiggyCreatures.Caching.Fusion;

namespace Web.BFF.Services;

public interface ITenantRoutingMetadataService
{
    Task<TenantRoutingMetadata?> GetTenantRoutingMetadataAsync(string tenantId, CancellationToken ct = default);
}

public sealed record TenantRoutingMetadata(string TenantId, string DatabaseStrategy);

public sealed class TenantRoutingMetadataService : ITenantRoutingMetadataService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFusionCache _fusionCache;
    private readonly ILogger<TenantRoutingMetadataService> _logger;
    private readonly string _databaseInfoEndpointTemplate;

    public TenantRoutingMetadataService(
        IHttpClientFactory httpClientFactory,
        IFusionCache fusionCache,
        ILogger<TenantRoutingMetadataService> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _fusionCache = fusionCache;
        _logger = logger;
        _databaseInfoEndpointTemplate = configuration["Services:CustomerApi:TenantDatabaseInfoEndpoint"]
            ?? "api/v1/tenants/{tenantId}/database-info";
    }

    public async Task<TenantRoutingMetadata?> GetTenantRoutingMetadataAsync(string tenantId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return null;
        }

        var cacheKey = $"tenant-routing:{tenantId}";

        try
        {
            return await _fusionCache.GetOrSetAsync<TenantRoutingMetadata?>(
                cacheKey,
                async (context, cancellationToken) =>
                {
                    var client = _httpClientFactory.CreateClient("CustomerApi");
                    var endpoint = _databaseInfoEndpointTemplate.Replace("{tenantId}", tenantId, StringComparison.OrdinalIgnoreCase);
                    var response = await client.GetAsync(endpoint, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "Failed to resolve tenant routing metadata for tenant {TenantId}. Status code: {StatusCode}",
                            tenantId,
                            response.StatusCode);
                        return null;
                    }

                    var payload = await response.Content.ReadFromJsonAsync<TenantDatabaseInfoResponse>(cancellationToken: cancellationToken);
                    if (payload == null || string.IsNullOrWhiteSpace(payload.Strategy))
                    {
                        return null;
                    }

                    context.Options
                        .SetDuration(TimeSpan.FromMinutes(5))
                        .SetFailSafe(true)
                        .SetFactoryTimeouts(TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(1));

                    return new TenantRoutingMetadata(tenantId, payload.Strategy);
                },
                token: ct);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Error resolving tenant routing metadata for tenant {TenantId}", tenantId);
            return null;
        }
    }

    private sealed class TenantDatabaseInfoResponse
    {
        [JsonPropertyName("strategy")]
        public string Strategy { get; set; } = string.Empty;
    }
}