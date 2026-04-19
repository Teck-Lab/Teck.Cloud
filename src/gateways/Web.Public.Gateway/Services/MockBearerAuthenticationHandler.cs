using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Web.Public.Gateway.Services;

internal sealed class MockBearerAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string NoTenantToken = "e2e-edge-no-tenant";
    private const string TenantNoRoleToken = "e2e-edge-tenant-no-role";
    private const string TenantEmployeeToken = "e2e-edge-tenant-employee";

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
            NoTenantToken =>
            [
                new Claim(ClaimTypes.NameIdentifier, "e2e-user"),
                new Claim(ClaimTypes.Name, "E2E User"),
            ],
            TenantNoRoleToken =>
            [
                new Claim(ClaimTypes.NameIdentifier, "e2e-user"),
                new Claim(ClaimTypes.Name, "E2E User"),
                new Claim("organization", "e2e-tenant"),
                new Claim("tenant_id", "e2e-tenant"),
            ],
            TenantEmployeeToken =>
            [
                new Claim(ClaimTypes.NameIdentifier, "e2e-user"),
                new Claim(ClaimTypes.Name, "E2E User"),
                new Claim("organization", "e2e-tenant"),
                new Claim("tenant_id", "e2e-tenant"),
                new Claim(ClaimTypes.Role, "employee"),
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
