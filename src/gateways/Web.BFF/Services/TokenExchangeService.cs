using System.Security.Cryptography;
using System.Text;
using IdentityModel.Client;

namespace Web.BFF.Services
{
    public interface ITokenExchangeService
    {
        Task<TokenResult> ExchangeTokenAsync(string subjectToken, string audience, string tenantId, CancellationToken ct = default);
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

                    var response = await client.RequestTokenExchangeTokenAsync(
                        new TokenExchangeTokenRequest
                        {
                            Address = tokenEndpoint,
                            ClientId = _config["Keycloak:GatewayClientId"] ?? string.Empty,
                            ClientSecret = _config["Keycloak:GatewayClientSecret"] ?? string.Empty,
                            SubjectToken = subjectToken,
                            SubjectTokenType = "urn:ietf:params:oauth:token-type:access_token",
                            Audience = audience
                        },
                        ct2);

                    if (response.IsError)
                    {
                        throw new HttpRequestException($"Token exchange failed: {response.Error}");
                    }

                    var access = response.AccessToken;
                    var expiresIn = response.ExpiresIn;

                    if (string.IsNullOrWhiteSpace(access))
                    {
                        throw new HttpRequestException("Token exchange failed: access_token is missing");
                    }

                    if (expiresIn <= 0)
                    {
                        throw new HttpRequestException("Token exchange failed: expires_in is missing or invalid");
                    }

                    var expiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
                    context.Options.Duration = TimeSpan.FromSeconds(Math.Max(30, expiresIn - 60));

                    return new TokenResult(access!, expiresAt);
                },
                token: ct);


        }

    }
}
