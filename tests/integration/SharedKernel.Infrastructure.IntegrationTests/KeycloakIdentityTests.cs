using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Keycloak.AuthServices.Authorization.AuthorizationServer;
using Keycloak.AuthServices.Common.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SharedKernel.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Real Keycloak exchange and Authorization Services, independent of Aspire.</summary>
/// <param name="fixture">The owned disposable identity provider.</param>
public sealed class KeycloakIdentityTests(KeycloakFixture fixture) : IClassFixture<KeycloakFixture>
{
    /// <summary>Delegation preserves the subject, narrows the audience and requires a real policy grant.</summary>
    /// <param name="username">The test-only login identity.</param>
    /// <param name="allowed">The policy's expected decision.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData("reader", true)]
    [InlineData("denied", false)]
    public async Task ExchangePreservesIdentityAndPermissionDecisions(string username, bool allowed)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var subject = await fixture.LoginAsync(username, cancellation).ConfigureAwait(true);
        using var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15), BaseAddress = new Uri(fixture.TokenEndpoint, "/realms/teck-local/") };
        var tokens = new OAuthTokenClient(http, fixture.GatewayOptions());
        var exchanged = await tokens.ExchangeAsync("integration-service", subject, cancellation).ConfigureAwait(true);
        using var original = DecodePayload(subject);
        using var delegated = DecodePayload(exchanged.AccessToken);
        delegated.RootElement.GetProperty("sub").GetString().ShouldBe(original.RootElement.GetProperty("sub").GetString());
        delegated.RootElement.GetProperty("aud").GetString().ShouldBe("integration-service");
        delegated.RootElement.GetProperty("iss").GetString().ShouldBe(original.RootElement.GetProperty("iss").GetString());
        var authorizationOptions = new KeycloakAuthorizationServerOptions
        {
            AuthServerUrl = new Uri(fixture.TokenEndpoint, "/").ToString(),
            Realm = "teck-local",
            Resource = "integration-service",
        };
        IAuthorizationServerClient authorization = new AuthorizationServerClient(http, Options.Create(authorizationOptions), NullLogger<AuthorizationServerClient>.Instance);
        (await authorization.VerifyAccessToResource("records", "read", exchanged.AccessToken, cancellation).ConfigureAwait(true)).ShouldBe(allowed);
        (await authorization.VerifyAccessToResource("records", "write", exchanged.AccessToken, cancellation).ConfigureAwait(true)).ShouldBeFalse();
    }

    /// <summary>Incorrect secrets and unauthorized audiences cannot produce delegated credentials.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task ExchangeRejectsWrongSecretAndAudience()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var subject = await fixture.LoginAsync("reader", cancellation).ConfigureAwait(true);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var wrongSecret = new OAuthTokenClient(http, fixture.GatewayOptions("incorrect-test-only-secret"));
        var error = await Should.ThrowAsync<HttpRequestException>(() => wrongSecret.ExchangeAsync("integration-service", subject, cancellation)).ConfigureAwait(true);
        error.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var tokens = new OAuthTokenClient(http, fixture.GatewayOptions());
        _ = await Should.ThrowAsync<HttpRequestException>(() => tokens.ExchangeAsync("admin-gateway", subject, cancellation)).ConfigureAwait(true);
        _ = await Should.ThrowAsync<HttpRequestException>(() => tokens.ExchangeAsync("integration-service", "invalid-token", cancellation)).ConfigureAwait(true);
    }

    /// <summary>Selected organization UUID survives real user-token exchange and upstream claim parsing.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task ExchangeRetainsSelectedOrganizationMembership()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var subject = await fixture.LoginAsync("reader", cancellation, "openid organization:fixture").ConfigureAwait(true);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var tokens = new OAuthTokenClient(http, fixture.GatewayOptions());
        var exchanged = await tokens.ExchangeForOrganizationAsync("integration-service", subject, "fixture", cancellation).ConfigureAwait(true);
        using var original = DecodePayload(subject);
        using var delegated = DecodePayload(exchanged.AccessToken);
        var originalOrganization = original.RootElement.GetProperty("organization");
        var delegatedOrganization = delegated.RootElement.GetProperty("organization");
        originalOrganization.GetProperty("fixture").GetProperty("id").GetString().ShouldBe(fixture.OrganizationId);
        delegatedOrganization.GetProperty("fixture").GetProperty("id").GetString().ShouldBe(fixture.OrganizationId);
        var claims = new ClaimsPrincipal(new ClaimsIdentity([new Claim("organization", delegatedOrganization.GetRawText(), "JSON")], "fixture-token-inspection"));
        claims.GetOrganizations().ShouldHaveSingleItem().Id.ShouldBe(fixture.OrganizationId);
        claims.IsMemberOfById(Guid.NewGuid().ToString("D")).ShouldBeFalse();
    }

    private static JsonDocument DecodePayload(string token)
    {
        // Inspection only; production hosts must validate signatures, issuer, audience and lifetime.
        var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        return JsonDocument.Parse(Convert.FromBase64String(payload.PadRight((payload.Length + 3) / 4 * 4, '=')));
    }
}
