using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using SharedKernel.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Native JWT validation refreshes real provider keys without restarting its owning host.</summary>
/// <param name="fixture">A disposable provider dedicated to this test class.</param>
public sealed class KeycloakSigningRotationTests(KeycloakFixture fixture) : IClassFixture<KeycloakFixture>
{
    /// <summary>A new signing key is discovered while an existing unexpired token remains valid.</summary>
    /// <returns>The bounded real-provider verification.</returns>
    [Fact]
    public async Task NativeHostRefreshesRotatedSigningKeysAndRejectsForgedSignatures()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var previous = await fixture.LoginAsync("reader", cancellation, "openid organization:fixture").ConfigureAwait(true);
        using var host = await CreateHostAsync(cancellation).ConfigureAwait(true);
        using var client = host.GetTestClient();
        (await SendAsync(client, previous, cancellation).ConfigureAwait(true)).ShouldBe(HttpStatusCode.OK);
        using var realm = await fixture.AdminAsync(HttpMethod.Get, string.Empty, body: null, cancellation).ConfigureAwait(true);
        var definition = new JsonObject
        {
            ["name"] = "fixture-rotated-signing-key",
            ["parentId"] = realm.RootElement.GetProperty("id").GetString(),
            ["providerId"] = "rsa-generated",
            ["providerType"] = "org.keycloak.keys.KeyProvider",
            ["config"] = new JsonObject
            {
                ["priority"] = new JsonArray("1000"),
                ["enabled"] = new JsonArray("true"),
                ["active"] = new JsonArray("true"),
                ["algorithm"] = new JsonArray("RS256"),
                ["keySize"] = new JsonArray("2048"),
            },
        };
        using var rotated = await fixture.AdminAsync(HttpMethod.Post, "components", definition, cancellation).ConfigureAwait(true);
        var current = await fixture.LoginAsync("reader", cancellation, "openid organization:fixture").ConfigureAwait(true);
        new JsonWebToken(current).Kid.ShouldNotBe(new JsonWebToken(previous).Kid, StringComparer.Ordinal);
        await VerifyPublishedKeysAsync(fixture.ServerAddress, new JsonWebToken(previous).Kid, new JsonWebToken(current).Kid, cancellation).ConfigureAwait(true);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        while (await SendAsync(client, current, deadline.Token).ConfigureAwait(true) != HttpStatusCode.OK)
        {
            await Task.Delay(200, deadline.Token).ConfigureAwait(true);
        }

        (await SendAsync(client, previous, cancellation).ConfigureAwait(true)).ShouldBe(HttpStatusCode.OK);
        var parts = current.Split('.');
        parts[2] = (parts[2][0] == 'A' ? "B" : "A") + parts[2][1..];
        (await SendAsync(client, string.Join('.', parts), cancellation).ConfigureAwait(true)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task VerifyPublishedKeysAsync(Uri address, string previous, string current, CancellationToken cancellationToken)
    {
        using var http = new HttpClient { BaseAddress = address, Timeout = TimeSpan.FromSeconds(10) };
        using var response = await http.GetAsync("realms/teck-local/protocol/openid-connect/certs", cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var keys = document.RootElement.GetProperty("keys").EnumerateArray().Select(key => key.GetProperty("kid").GetString()).ToArray();
        keys.ShouldContain(previous, StringComparer.Ordinal);
        keys.ShouldContain(current, StringComparer.Ordinal);
    }

    private static async Task<HttpStatusCode> SendAsync(HttpClient client, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.StatusCode;
    }

    private Task<IHost> CreateHostAsync(CancellationToken cancellationToken)
    {
        var builder = new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
        {
            _ = services.AddRouting();
            _ = services.AddFoundationKeycloakAuthentication(new KeycloakServiceOptions
            {
                ServerUrl = fixture.ServerAddress,
                Realm = "teck-local",
                Audience = "customer-gateway",
                Transport = IdentityTransport.IsolatedDevelopment,
            });
            _ = services.AddAuthorization();
        }).Configure(app =>
        {
            _ = app.UseRouting();
            _ = app.UseAuthentication();
            _ = app.UseAuthorization();
            _ = app.UseEndpoints(endpoints => endpoints.MapGet("/", context => context.Response.WriteAsync("accepted", context.RequestAborted)).RequireAuthorization());
        }));
        return builder.StartAsync(cancellationToken);
    }
}
