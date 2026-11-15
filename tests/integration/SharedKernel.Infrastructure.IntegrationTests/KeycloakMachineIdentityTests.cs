using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Keycloak.AuthServices.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Machine identities obtain their own tokens and cannot claim a tenant through a request header.</summary>
/// <param name="fixture">The owned disposable identity provider.</param>
public sealed class KeycloakMachineIdentityTests(KeycloakFixture fixture) : IClassFixture<KeycloakFixture>
{
    /// <summary>A real client-credentials token requires actual organization membership at the gateway.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task ClientCredentialsRequireOrganizationMembership()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var secret = RandomNumberGenerator.GetHexString(48);
        var serviceAccount = await ProvisionClientAsync(secret, cancellation).ConfigureAwait(true);
        using var host = await CreateHostAsync(cancellation).ConfigureAwait(true);
        using var gateway = host.GetTestClient();
        var unbound = await GrantAsync(secret, scope: null, cancellation).ConfigureAwait(true);
        (await GatewayStatusAsync(gateway, unbound, cancellation).ConfigureAwait(true)).ShouldBe(HttpStatusCode.Forbidden);

        using var membership = await fixture.AdminAsync(HttpMethod.Post, "organizations/" + fixture.OrganizationId + "/members", JsonValue.Create(serviceAccount), cancellation).ConfigureAwait(true);
        var bound = await GrantAsync(secret, "organization:fixture", cancellation).ConfigureAwait(true);
        (await GatewayStatusAsync(gateway, bound, cancellation).ConfigureAwait(true)).ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<HttpStatusCode> GatewayStatusAsync(HttpClient gateway, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Tenant-Id", "forged-tenant");
        using var response = await gateway.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.StatusCode;
    }

    private Task<IHost> CreateHostAsync(CancellationToken cancellationToken)
    {
        return new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
        {
            _ = services.AddRouting();
            _ = services.AddFoundationKeycloakAuthentication(new KeycloakServiceOptions
            {
                ServerUrl = fixture.ServerAddress,
                Realm = "teck-local",
                Audience = "customer-gateway",
                Transport = IdentityTransport.IsolatedDevelopment,
            });
            _ = services.AddAuthorizationBuilder().AddPolicy("member", policy => policy.RequireAuthenticatedUser().RequireOrganizationMembership(fixture.OrganizationId));
        }).Configure(app =>
        {
            _ = app.UseRouting();
            _ = app.UseAuthentication();
            _ = app.UseAuthorization();
            _ = app.UseEndpoints(endpoints => endpoints.MapGet("/", () => Results.Ok()).RequireAuthorization("member"));
        })).StartAsync(cancellationToken);
    }

    private async Task<string> GrantAsync(string secret, string? scope, CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "external-fixture",
            ["client_secret"] = secret,
        };
        if (scope is not null)
        {
            fields["scope"] = scope;
        }

        using var body = new FormUrlEncodedContent(fields);
        using var response = await http.PostAsync(fixture.TokenEndpoint, body, cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var disposeStream = stream.ConfigureAwait(false);
        using var token = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return token.RootElement.GetProperty("access_token").GetString()!;
    }

    private async Task<string> ProvisionClientAsync(string secret, CancellationToken cancellationToken)
    {
        var definition = JsonNode.Parse("""{"clientId":"external-fixture","enabled":true,"publicClient":false,"standardFlowEnabled":false,"directAccessGrantsEnabled":false,"serviceAccountsEnabled":true,"defaultClientScopes":["basic","roles"],"optionalClientScopes":["organization"],"protocolMappers":[{"name":"gateway","protocol":"openid-connect","protocolMapper":"oidc-audience-mapper","config":{"included.client.audience":"customer-gateway","access.token.claim":"true"}}]}""")!.AsObject();
        definition["secret"] = secret;
        using var created = await fixture.AdminAsync(HttpMethod.Post, "clients", definition, cancellationToken).ConfigureAwait(false);
        using var clients = await fixture.AdminAsync(HttpMethod.Get, "clients?clientId=external-fixture", body: null, cancellationToken).ConfigureAwait(false);
        var clientId = clients.RootElement[0].GetProperty("id").GetString();
        using var account = await fixture.AdminAsync(HttpMethod.Get, "clients/" + clientId + "/service-account-user", body: null, cancellationToken).ConfigureAwait(false);
        return account.RootElement.GetProperty("id").GetString()!;
    }
}
