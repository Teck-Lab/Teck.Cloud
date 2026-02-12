using System.Security.Cryptography;
using System.Text;

namespace Web.BFF.Services
{
    public interface ITokenExchangeService
    {
        Task<TokenResult> ExchangeTokenAsync(string subjectToken, string audience, string tenantId, CancellationToken ct = default);
        Task<TokenResult> SwitchOrganizationAsync(string subjectToken, string orgId, CancellationToken ct = default);
    }

    public record TokenResult(string AccessToken, DateTime ExpiresAt);

    public class TokenExchangeService : ITokenExchangeService
    {
private readonly IHttpClientFactory _httpClientFactory;
        private readonly ZiggyCreatures.Caching.Fusion.IFusionCache _fusionCache;
        private readonly IConfiguration _config;

        public TokenExchangeService(IHttpClientFactory httpClientFactory, ZiggyCreatures.Caching.Fusion.IFusionCache fusionCache, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _fusionCache = fusionCache;
            _config = config;
        }

        private static string Sha256(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes);
        }

        public async Task<TokenResult> ExchangeTokenAsync(string subjectToken, string audience, string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(subjectToken)) throw new ArgumentNullException(nameof(subjectToken));
            if (string.IsNullOrEmpty(audience)) throw new ArgumentNullException(nameof(audience));
var key = $"token:{Sha256(subjectToken)}:{audience}:{tenantId}";

            return await _fusionCache.GetOrSetAsync<TokenResult>(
                key,
                async (context, ct2) =>
                {
                    var client = _httpClientFactory.CreateClient("KeycloakTokenClient");
                    var tokenEndpoint = _config["Keycloak:TokenEndpoint"] ?? _config["Keycloak:Authority"] + "/protocol/openid-connect/token";

                    var req = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
                    req.Content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:token-exchange"),
                        new KeyValuePair<string, string>("subject_token", subjectToken),
                        new KeyValuePair<string, string>("subject_token_type", "urn:ietf:params:oauth:token-type:access_token"),
                        new KeyValuePair<string, string>("audience", audience),
                        new KeyValuePair<string, string>("client_id", _config["Keycloak:GatewayClientId"] ?? string.Empty),
                        new KeyValuePair<string, string>("client_secret", _config["Keycloak:GatewayClientSecret"] ?? string.Empty),
                    });

                    var res = await client.SendAsync(req, ct2);
                    res.EnsureSuccessStatusCode();
                    var json = await res.Content.ReadAsStringAsync(ct2);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var access = doc.RootElement.GetProperty("access_token").GetString();
                    var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();

                    var expiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
                    context.Options.Duration = TimeSpan.FromSeconds(Math.Max(30, expiresIn - 60));

                    return new TokenResult(access!, expiresAt);
                },
                token: ct);


        }

        public async Task<TokenResult> SwitchOrganizationAsync(string subjectToken, string orgId, CancellationToken ct = default)
        {
            var client = _httpClientFactory.CreateClient("KeycloakTokenClient");
            var tokenEndpoint = _config["Keycloak:Authority"] + "/protocol/openid-connect/token";

            var req = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
            req.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:token-exchange"),
                new KeyValuePair<string, string>("subject_token", subjectToken),
                new KeyValuePair<string, string>("subject_token_type", "urn:ietf:params:oauth:token-type:access_token"),
                new KeyValuePair<string, string>("audience", _config["Keycloak:GatewayClientId"] ?? string.Empty),
                new KeyValuePair<string, string>("client_id", _config["Keycloak:GatewayClientId"] ?? string.Empty),
                new KeyValuePair<string, string>("client_secret", _config["Keycloak:GatewayClientSecret"] ?? string.Empty),
                new KeyValuePair<string, string>("org_id", orgId)
            });

            var res = await client.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync(ct);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var access = doc.RootElement.GetProperty("access_token").GetString();
            var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();

            var expiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
            return new TokenResult(access!, expiresAt);
        }
    }
}
