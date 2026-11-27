using Microsoft.AspNetCore.Http;
using SharedKernel.Infrastructure.Auth;

namespace Teck.Cloud.Commerce.Contracts;

/// <summary>Exchanges the authorized staff token for the Orders audience without an organization scope.</summary>
/// <param name="accessor">Provides the current HTTP context carrying the caller's validated token.</param>
/// <param name="tokens">The OAuth client used to exchange the original token.</param>
internal sealed class AdminGatewayTokenProvider(IHttpContextAccessor accessor, OAuthTokenClient tokens) : IAccessTokenProvider
{
    /// <inheritdoc/>
    public Task<string> GetTokenAsync(string audience, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        return string.Equals(audience, CommerceAuth.OrdersAudience, StringComparison.Ordinal)
            ? ExchangeAsync(audience, cancellationToken)
            : throw new InvalidOperationException("The admin gateway has no commerce credential for the requested audience.");
    }

    private async Task<string> ExchangeAsync(string audience, CancellationToken cancellationToken)
    {
        var http = CommerceAuth.RequireValidatedContext(accessor);
        var exchanged = await tokens.ExchangeAsync(audience, CommerceAuth.OriginToken(http), cancellationToken).ConfigureAwait(false);
        return exchanged.AccessToken;
    }
}
