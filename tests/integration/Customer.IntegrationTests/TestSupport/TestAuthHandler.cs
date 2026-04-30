using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Customer.IntegrationTests.TestSupport;

internal sealed class TestAuthHandlerOptions : AuthenticationSchemeOptions
{
}

internal sealed class TestAuthHandler : AuthenticationHandler<TestAuthHandlerOptions>
{
    public const string SchemeName = "TestAuth";
    public const string TenantIdClaimHeaderName = "X-Test-TenantIdClaim";
    public const string ScopeHeaderName = "X-Test-Scopes";

    public TestAuthHandler(
        IOptionsMonitor<TestAuthHandlerOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (this.Request.Headers.Authorization.Count == 0)
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing bearer token."));
        }

        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, "integration-user"),
            new Claim(ClaimTypes.Name, "Integration User"),
        ];

        if (this.Request.Headers.TryGetValue(TenantIdClaimHeaderName, out var tenantClaimValues))
        {
            claims.Add(new Claim("tenant_id", tenantClaimValues.ToString()));
        }

        if (this.Request.Headers.TryGetValue(ScopeHeaderName, out var scopeValues))
        {
            claims.Add(new Claim("scope", scopeValues.ToString()));
        }

        ClaimsIdentity identity = new(claims, SchemeName);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
