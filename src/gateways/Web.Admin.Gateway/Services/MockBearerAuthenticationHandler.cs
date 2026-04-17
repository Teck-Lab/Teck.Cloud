using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Web.Admin.Gateway.Services;

internal sealed class MockBearerAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string PlatformAdminToken = "e2e-admin-platform-admin";

    public MockBearerAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? authorizationHeader = this.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorizationHeader) ||
            !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string token = authorizationHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing bearer token."));
        }

        List<Claim>? claims = token switch
        {
            PlatformAdminToken =>
            [
                new Claim(ClaimTypes.NameIdentifier, "e2e-admin-user"),
                new Claim(ClaimTypes.Name, "E2E Platform Admin"),
                new Claim(ClaimTypes.Role, "platform-admin"),
            ],
            _ => null,
        };

        if (claims is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Unknown test token."));
        }

        ClaimsIdentity identity = new(claims, this.Scheme.Name);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, this.Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
