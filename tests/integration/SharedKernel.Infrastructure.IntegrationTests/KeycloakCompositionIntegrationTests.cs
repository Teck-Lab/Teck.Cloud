using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Duende.AccessTokenManagement;
using Keycloak.AuthServices.Authorization;
using Keycloak.AuthServices.Sdk.Admin;
using Keycloak.AuthServices.Sdk.Kiota.Admin;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Kiota.Abstractions;
using SharedKernel.Infrastructure.Auth;
using Shouldly;
using Xunit;
using AdminOptions = Keycloak.AuthServices.Sdk.Kiota.KeycloakAdminClientOptions;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Real provider proof for native host authentication and realm-local service-account composition.</summary>
/// <param name="fixture">This class's owned disposable Keycloak instance.</param>
public sealed class KeycloakCompositionIntegrationTests(KeycloakFixture fixture) : IClassFixture<KeycloakFixture>
{
    /// <summary>Native JWT and organization requirements accept only authenticated members of the expected organization.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task NativeHostValidatesSignatureAudienceIssuerAndOrganization()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var token = await fixture.LoginAsync("reader", cancellation, "openid organization:fixture").ConfigureAwait(true);
        var denied = await fixture.LoginAsync("denied", cancellation).ConfigureAwait(true);
        using var host = await CreateHostAsync("customer-gateway", cancellation).ConfigureAwait(true);
        using var http = host.GetTestClient();
        (await SendAsync(http, token, cancellation).ConfigureAwait(true)).ShouldBe(HttpStatusCode.OK);
        (await SendAsync(http, denied, cancellation).ConfigureAwait(true)).ShouldBe(HttpStatusCode.Forbidden);
        var parts = token.Split('.');
        parts[2] = (parts[2][0] == 'A' ? "B" : "A") + parts[2][1..];
        (await SendAsync(http, string.Join('.', parts), cancellation).ConfigureAwait(true)).ShouldBe(HttpStatusCode.Unauthorized);
        using var wrongAudience = await CreateHostAsync("unrelated-service", cancellation).ConfigureAwait(true);
        using var audienceHttp = wrongAudience.GetTestClient();
        (await SendAsync(audienceHttp, token, cancellation).ConfigureAwait(true)).ShouldBe(HttpStatusCode.Unauthorized);
        using var wrongIssuer = await CreateHostAsync("customer-gateway", cancellation, new Uri(fixture.ServerAddress, "realms/unrelated")).ConfigureAwait(true);
        using var issuerHttp = wrongIssuer.GetTestClient();
        (await SendAsync(issuerHttp, token, cancellation).ConfigureAwait(true)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>The application authenticates through Duende using a realm-local read-only account, never the fixture administrator.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task NativeAdminServiceAccountReadsButCannotWriteOrganizations()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var options = await ProvisionReaderAsync(cancellation).ConfigureAwait(true);
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddFoundationKeycloakAdmin(options, IdentityTransport.IsolatedDevelopment);
        await using var provider = services.BuildServiceProvider();
        var manager = provider.GetRequiredService<IClientCredentialsTokenManager>();
        var credential = await manager.GetAccessTokenAsync(ClientCredentialsClientName.Parse("teck-keycloak-admin"), parameters: null, ct: cancellation).ConfigureAwait(true);
        credential.Succeeded.ShouldBeTrue(credential.FailedResult?.Error);
        var issuedToken = credential.Token.ShouldNotBeNull().AccessToken.ToString();
        var parsedToken = new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(issuedToken);
        parsedToken.Subject.ShouldNotBeNullOrEmpty();
        parsedToken.Audiences.ShouldContain(value => string.Equals(value, "realm-management", StringComparison.Ordinal));
        parsedToken.Issuer.ShouldBe(new Uri(fixture.ServerAddress, "realms/teck-local").ToString());
        parsedToken.Audiences.ShouldContain(value => string.Equals(value, new Uri(fixture.ServerAddress, "admin/realms/teck-local").ToString(), StringComparison.Ordinal));
        using var direct = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        using var directRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(fixture.ServerAddress, "admin/realms/teck-local/organizations/" + fixture.OrganizationId));
        directRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", issuedToken);
        using var directResponse = await direct.SendAsync(directRequest, cancellation).ConfigureAwait(true);
        directResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var client = provider.GetRequiredService<KeycloakAdminApiClient>();
        var organization = await client.Admin.Realms["teck-local"].Organizations[fixture.OrganizationId].GetAsync(cancellationToken: cancellation).ConfigureAwait(true);
        organization.ShouldNotBeNull().Id.ShouldBe(fixture.OrganizationId);
        organization.Enabled.ShouldBe(expected: true);
        var error = await Should.ThrowAsync<ApiException>(() => client.Admin.Realms["teck-local"].Organizations[fixture.OrganizationId].PutAsync(organization, cancellationToken: cancellation)).ConfigureAwait(true);
        error.ResponseStatusCode.ShouldBe(403);
        var handwritten = provider.GetRequiredService<IKeycloakClient>();
        using var forbiddenRealm = await handwritten.GetRealmWithResponseAsync("master", cancellation).ConfigureAwait(true);
        forbiddenRealm.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<HttpStatusCode> SendAsync(HttpClient http, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.StatusCode;
    }

    private Task<IHost> CreateHostAsync(string audience, CancellationToken cancellationToken, Uri? issuer = null)
    {
        var builder = new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
        {
            _ = services.AddRouting();
            _ = services.AddFoundationKeycloakAuthentication(new KeycloakServiceOptions
            {
                ServerUrl = fixture.ServerAddress,
                Realm = "teck-local",
                Audience = audience,
                PublicIssuer = issuer,
                Transport = IdentityTransport.IsolatedDevelopment,
            });
            _ = services.AddAuthorizationBuilder().AddPolicy("member", policy => policy.RequireAuthenticatedUser().RequireOrganizationMembership(fixture.OrganizationId));
        }).Configure(app =>
        {
            _ = app.UseRouting();
            _ = app.UseAuthentication();
            _ = app.UseAuthorization();
            _ = app.UseEndpoints(endpoints => endpoints.MapGet("/", context => context.Response.WriteAsync("accepted", context.RequestAborted)).RequireAuthorization("member"));
        }));
        return builder.StartAsync(cancellationToken);
    }

    private async Task<AdminOptions> ProvisionReaderAsync(CancellationToken cancellationToken)
    {
        var secret = RandomNumberGenerator.GetHexString(48);
        var definition = new JsonObject
        {
            ["clientId"] = "organization-reader",
            ["enabled"] = true,
            ["publicClient"] = false,
            ["secret"] = secret,
            ["standardFlowEnabled"] = false,
            ["directAccessGrantsEnabled"] = false,
            ["serviceAccountsEnabled"] = true,
            ["defaultClientScopes"] = new JsonArray("basic", "roles"),
            ["optionalClientScopes"] = new JsonArray("kc-admin-api"),
        };
        using var created = await fixture.AdminAsync(HttpMethod.Post, "clients", definition, cancellationToken).ConfigureAwait(false);
        using var clients = await fixture.AdminAsync(HttpMethod.Get, "clients?clientId=organization-reader", body: null, cancellationToken).ConfigureAwait(false);
        var clientId = clients.RootElement[0].GetProperty("id").GetString();
        using var account = await fixture.AdminAsync(HttpMethod.Get, "clients/" + clientId + "/service-account-user", body: null, cancellationToken).ConfigureAwait(false);
        using var management = await fixture.AdminAsync(HttpMethod.Get, "clients?clientId=realm-management", body: null, cancellationToken).ConfigureAwait(false);
        var managementId = management.RootElement[0].GetProperty("id").GetString();
        using var role = await fixture.AdminAsync(HttpMethod.Get, "clients/" + managementId + "/roles/view-organizations", body: null, cancellationToken).ConfigureAwait(false);
        using var assigned = await fixture.AdminAsync(HttpMethod.Post, "users/" + account.RootElement.GetProperty("id").GetString() + "/role-mappings/clients/" + managementId, new JsonArray(JsonNode.Parse(role.RootElement.GetRawText())), cancellationToken).ConfigureAwait(false);
        return new AdminOptions
        {
            AuthServerUrl = fixture.ServerAddress.ToString(),
            Realm = "teck-local",
            Resource = "organization-reader",
            Credentials = new Keycloak.AuthServices.Common.KeycloakClientInstallationCredentials { Secret = secret },
        };
    }
}
