using Microsoft.AspNetCore.Http;
using SharedKernel.Infrastructure.Auth;

namespace Teck.Cloud.Commerce.Contracts;

/// <summary>Preserves the caller's organization scope when delegating to Orders and uses a workload token for Licensing.</summary>
/// <param name="accessor">Provides the current HTTP context carrying the caller's validated token and organization alias.</param>
/// <param name="tokens">The OAuth client used to exchange or mint the requested token.</param>
internal sealed class CustomerGatewayTokenProvider(IHttpContextAccessor accessor, OAuthTokenClient tokens) : IAccessTokenProvider
{
    /// <inheritdoc/>
    public Task<string> GetTokenAsync(string audience, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        return audience switch
        {
            CommerceAuth.OrdersAudience => ExchangeForOrdersAsync(audience, cancellationToken),
            CommerceAuth.LicensingAudience => ClientCredentialsAsync(audience, cancellationToken),
            _ => throw new InvalidOperationException("The customer gateway has no commerce credential for the requested audience."),
        };
    }

    private async Task<string> ExchangeForOrdersAsync(string audience, CancellationToken cancellationToken)
    {
        var http = CommerceAuth.RequireValidatedContext(accessor);
        var exchanged = await tokens.ExchangeForOrganizationAsync(
            audience,
            CommerceAuth.OriginToken(http),
            CommerceAuth.SelectedOrganizationAlias(http),
            cancellationToken).ConfigureAwait(false);
        return exchanged.AccessToken;
    }

    private async Task<string> ClientCredentialsAsync(string audience, CancellationToken cancellationToken) =>
        (await tokens.ClientCredentialsAsync(audience, cancellationToken).ConfigureAwait(false)).AccessToken;
}
