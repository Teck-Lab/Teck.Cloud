using SharedKernel.Infrastructure.Auth;

namespace Teck.Cloud.Commerce.Contracts;

/// <summary>Obtains the scoped Licensing workload token for the internal Orders entitlement check.</summary>
/// <param name="tokens">The OAuth client used to request the Orders workload token.</param>
internal sealed class LicensingTokenProvider(OAuthTokenClient tokens) : IAccessTokenProvider
{
    /// <inheritdoc/>
    public async Task<string> GetTokenAsync(string audience, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        return !string.Equals(audience, CommerceAuth.OrdersAudience, StringComparison.Ordinal)
            ? throw new InvalidOperationException("Licensing has no commerce credential for the requested audience.")
            : (await tokens.ClientCredentialsAsync(audience, cancellationToken).ConfigureAwait(false)).AccessToken;
    }
}
