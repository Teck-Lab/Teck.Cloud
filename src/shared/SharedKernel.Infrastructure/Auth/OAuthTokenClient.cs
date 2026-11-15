using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SharedKernel.Infrastructure.Auth;

/// <summary>Asynchronous OAuth client credentials and explicit RFC 8693 token exchange.</summary>
/// <param name="http">Factory-managed client with a bounded timeout.</param>
/// <param name="options">Trusted endpoint and client credentials.</param>
public sealed partial class OAuthTokenClient(HttpClient http, OAuthClientOptions options)
{
    /// <summary>Obtains a service token for background operations.</summary>
    /// <param name="audience">The explicit service audience.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>A token response, with expiry, for a service-owned token cache.</returns>
    public Task<TokenResponse> ClientCredentialsAsync(string audience, CancellationToken cancellationToken = default) =>
        RequestAsync(audience, subjectToken: null, scope: null, cancellationToken);

    /// <summary>Exchanges an authenticated subject token for a downstream audience.</summary>
    /// <param name="audience">The explicit downstream audience.</param>
    /// <param name="subjectToken">An authenticated token; never log or persist it.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The token response.</returns>
    public Task<TokenResponse> ExchangeAsync(string audience, string subjectToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectToken);
        return RequestAsync(audience, subjectToken, scope: null, cancellationToken);
    }

    /// <summary>Preserves an already authorized organization when delegating a user token.</summary>
    /// <param name="audience">The downstream service audience.</param>
    /// <param name="subjectToken">The validated incoming user access token.</param>
    /// <param name="organizationAlias">The selected organization from Keycloak.AuthServices' validated claims, never a raw header.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>A delegated token scoped to the selected organization.</returns>
    /// <exception cref="ArgumentException">The alias is empty, ambiguous or contains scope delimiters.</exception>
    public Task<TokenResponse> ExchangeForOrganizationAsync(string audience, string subjectToken, string organizationAlias, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationAlias);
        return organizationAlias.Any(char.IsWhiteSpace) || organizationAlias.Contains('*', StringComparison.Ordinal) || organizationAlias.Contains(':', StringComparison.Ordinal)
            ? throw new ArgumentException("An explicit, single organization alias is required.", nameof(organizationAlias))
            : RequestAsync(audience, subjectToken, "organization:" + organizationAlias, cancellationToken);
    }

    private Task<TokenResponse> RequestAsync(string audience, string? subjectToken, string? scope, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            throw new InvalidOperationException("OAuth client credentials are required.");
        }

        IdentityTransportPolicy.Validate(options.TokenEndpoint, options.Transport);

        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
            ["audience"] = audience,
            ["grant_type"] = subjectToken is null ? "client_credentials" : "urn:ietf:params:oauth:grant-type:token-exchange",
        };
        if (subjectToken is not null)
        {
            fields["subject_token"] = subjectToken;
            fields["subject_token_type"] = "urn:ietf:params:oauth:token-type:access_token";
            fields["requested_token_type"] = "urn:ietf:params:oauth:token-type:access_token";
        }

        if (scope is not null)
        {
            fields["scope"] = scope;
        }

        return SendAsync(fields, cancellationToken);
    }

    private async Task<TokenResponse> SendAsync(Dictionary<string, string> fields, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, options.TokenEndpoint) { Content = new FormUrlEncodedContent(fields) };
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync(TokenJson.Default.TokenResponse, cancellationToken).ConfigureAwait(false);
        return token is { ExpiresIn: > 0 } && !string.IsNullOrWhiteSpace(token.AccessToken)
            && string.Equals(token.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase)
            ? token
            : throw new InvalidOperationException("The identity provider returned an invalid token response.");
    }

    /// <summary>An OAuth response; never log the access token.</summary>
    public sealed class TokenResponse
    {
        /// <summary>Gets the bearer token.</summary>
        [JsonPropertyName("access_token")]
        public required string AccessToken { get; init; }

        /// <summary>Gets the token lifetime in seconds.</summary>
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }

        /// <summary>Gets the token type.</summary>
        [JsonPropertyName("token_type")]
        public required string TokenType { get; init; }

        /// <inheritdoc/>
        public override string ToString() => "TokenResponse { credentials redacted }";
    }

    [JsonSerializable(typeof(TokenResponse))]
    private sealed partial class TokenJson : JsonSerializerContext;
}
