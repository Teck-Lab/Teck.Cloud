using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Web.Edge.Security;

namespace Web.Edge.UnitTests;

public sealed class InternalIdentityTokenServiceTests
{
    [Fact]
    public void CreateToken_ReturnsNull_WhenSigningKeyMissing()
    {
        var configuration = new ConfigurationBuilder().Build();
        var service = new InternalIdentityTokenService(configuration, NullLogger<InternalIdentityTokenService>.Instance);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "user-1"),
        ], "test-auth"));

        var token = service.CreateToken(principal);

        token.ShouldBeNull();
    }

    [Fact]
    public void CreateToken_CreatesSignedJwt_WithExpectedIssuerAndAudience()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EdgeTrust:SigningKey"] = "0123456789abcdef0123456789abcdef",
                ["EdgeTrust:Issuer"] = "teck-edge",
                ["EdgeTrust:Audience"] = "teck-web-bff-internal",
            })
            .Build();

        var service = new InternalIdentityTokenService(configuration, NullLogger<InternalIdentityTokenService>.Instance);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "user-1"),
            new Claim("tenant_id", "tenant-a"),
            new Claim("role", "realm-admin"),
        ], "test-auth"));

        var token = service.CreateToken(principal);

        token.ShouldNotBeNullOrWhiteSpace();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Issuer.ShouldBe("teck-edge");
        jwt.Audiences.ShouldContain("teck-web-bff-internal");
        jwt.Claims.ShouldContain(claim => claim.Type == "sub" && claim.Value == "user-1");
        jwt.Claims.ShouldContain(claim => claim.Type == "tenant_id" && claim.Value == "tenant-a");
    }
}
