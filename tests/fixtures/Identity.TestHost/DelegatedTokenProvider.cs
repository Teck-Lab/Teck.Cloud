using Duende.AccessTokenManagement.OpenIdConnect;
using Keycloak.AuthServices.Common.Claims;
using Microsoft.AspNetCore.Authorization;
using SharedKernel.Infrastructure.Auth;

namespace Identity.TestHost;

internal sealed class DelegatedTokenProvider(IHttpContextAccessor accessor, OAuthTokenClient tokens, IConfiguration configuration, IAuthorizationService authorization) : IAccessTokenProvider
{
    public async Task<string> GetTokenAsync(string audience, CancellationToken cancellationToken)
    {
        var http = accessor.HttpContext ?? throw new InvalidOperationException("A browser request is required.");
        var administrator = string.Equals(configuration["Identity:ClientId"], "identity-admin-gateway", StringComparison.Ordinal);
        if (administrator && !(await authorization.AuthorizeAsync(http.User, http, "platform-admin").ConfigureAwait(false)).Succeeded)
        {
            throw new UnauthorizedAccessException("Fresh administrative authentication is required before delegation.");
        }

        var managed = await http.GetUserAccessTokenAsync(ct: cancellationToken).ConfigureAwait(false);
        var incoming = managed.Token?.AccessToken.ToString()
            ?? throw new InvalidOperationException("The authenticated session could not obtain a valid access token.");

        // Administrative targeting is authorized explicitly at each endpoint, independent of memberships.
        var delegated = administrator
            ? await tokens.ExchangeAsync(audience, incoming, cancellationToken).ConfigureAwait(false)
            : await tokens.ExchangeForOrganizationAsync(audience, incoming, http.User.GetOrganizations().Single().Alias, cancellationToken).ConfigureAwait(false);
        return delegated.AccessToken;
    }
}
