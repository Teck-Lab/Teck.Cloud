using System.Net;
using System.Text;
using SharedKernel.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.UnitTests;

/// <summary>Verifies cancellation and explicit OAuth grants with a controlled transport.</summary>
public sealed class OAuthTests
{
    /// <summary>Client credentials and token exchange use separate explicit grants.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task GrantsAndSecretsAreHandledExplicitly()
    {
        using var handler = new TokenHandler();
        using var http = new HttpClient(handler);
        var options = new OAuthClientOptions { TokenEndpoint = new Uri("https://identity.test/token"), ClientId = "fixture", ClientSecret = "test-only-secret" };
        var client = new OAuthTokenClient(http, options);
        var response = await client.ClientCredentialsAsync("billing", TestContext.Current.CancellationToken).ConfigureAwait(true);
        _ = handler.Body.ShouldNotBeNull();
        handler.Body.ShouldContain("grant_type=client_credentials", Case.Sensitive);
        handler.Body.ShouldContain("audience=billing", Case.Sensitive);
        response.ToString().ShouldNotContain("test-only-token", Case.Sensitive);
        options.ToString().ShouldNotContain(options.ClientSecret, Case.Sensitive);
        _ = await client.ExchangeAsync("customer", "subject-fixture", TestContext.Current.CancellationToken).ConfigureAwait(true);
        handler.Body.ShouldContain("subject_token=subject-fixture", Case.Sensitive);
        handler.Body.ShouldContain("token-exchange", Case.Sensitive);
    }

    /// <summary>Cancelled requests stop and invalid token replies are rejected.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task CancellationAndInvalidResponsesFail()
    {
        using var handler = new TokenHandler { Reply = "{}" };
        using var http = new HttpClient(handler);
        var client = new OAuthTokenClient(http, new OAuthClientOptions { TokenEndpoint = new Uri("https://identity.test/token"), ClientId = "fixture", ClientSecret = "test-only-secret" });
        _ = (await Should.ThrowAsync<System.Text.Json.JsonException>(() => client.ClientCredentialsAsync("service", TestContext.Current.CancellationToken)).ConfigureAwait(true)).ShouldBeOfType<System.Text.Json.JsonException>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);
        _ = await Should.ThrowAsync<OperationCanceledException>(() => client.ClientCredentialsAsync("service", cancelled.Token)).ConfigureAwait(true);
    }

    /// <summary>Plain HTTP requires an explicit isolated or mesh-protected transport.</summary>
    /// <param name="transport">The trusted deployment transport.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData(IdentityTransport.AmbientMesh)]
    [InlineData(IdentityTransport.IsolatedDevelopment)]
    public async Task ExplicitProtectedTransportAllowsHttp(IdentityTransport transport)
    {
        using var handler = new TokenHandler();
        using var http = new HttpClient(handler);
        var client = new OAuthTokenClient(http, new OAuthClientOptions
        {
            TokenEndpoint = new Uri("http://keycloak:8080/realms/teck/token"),
            ClientId = "fixture",
            ClientSecret = "test-only-secret",
            Transport = transport,
        });
        var response = await client.ClientCredentialsAsync("service", TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.ExpiresIn.ShouldBe(60);
    }

    /// <summary>Organization delegation cannot inject additional scopes or select multiple tenants.</summary>
    /// <param name="alias">An unsafe or ambiguous organization selector.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData("*")]
    [InlineData("alpha beta")]
    [InlineData("alpha:beta")]
    [InlineData("alpha\tbeta")]
    public async Task AmbiguousOrganizationScopeIsRejectedBeforeNetwork(string alias)
    {
        using var handler = new TokenHandler();
        using var http = new HttpClient(handler);
        var client = new OAuthTokenClient(http, new OAuthClientOptions { TokenEndpoint = new Uri("https://identity.test/token"), ClientId = "fixture", ClientSecret = "fixture-secret" });
        _ = await Should.ThrowAsync<ArgumentException>(() => client.ExchangeForOrganizationAsync("service", "subject", alias, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        handler.Body.ShouldBeNull();
    }

    /// <summary>Transport defaults reject cleartext credential delivery before making a request.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task DefaultTransportRejectsHttp()
    {
        using var handler = new TokenHandler();
        using var http = new HttpClient(handler);
        var client = new OAuthTokenClient(http, new OAuthClientOptions { TokenEndpoint = new Uri("http://identity.test/token"), ClientId = "fixture", ClientSecret = "fixture-secret" });
        _ = await Should.ThrowAsync<InvalidOperationException>(() => client.ClientCredentialsAsync("service", TestContext.Current.CancellationToken)).ConfigureAwait(true);
        handler.Body.ShouldBeNull();
    }

    private sealed class TokenHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        public string Reply { get; init; } = """{"access_token":"test-only-token","token_type":"Bearer","expires_in":60}""";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Reply, Encoding.UTF8, "application/json") };
        }
    }
}
