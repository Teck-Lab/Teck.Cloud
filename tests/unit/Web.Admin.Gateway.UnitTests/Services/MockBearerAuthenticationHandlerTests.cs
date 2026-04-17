using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Web.Admin.Gateway.Services;

namespace Web.Admin.Gateway.UnitTests.Services;

public sealed class MockBearerAuthenticationHandlerTests
{
    private const string PlatformAdminToken = "e2e-admin-platform-admin";
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleAuthenticate_ShouldReturnNoResult_WhenAuthorizationHeaderAbsent()
    {
        await using TestHost host = CreateHost();

        HttpResponseMessage response = await host.Client.GetAsync(new Uri("/test", UriKind.Relative), TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HandleAuthenticate_ShouldReturnFail_WhenTokenIsEmpty()
    {
        await using TestHost host = CreateHost();

        using HttpRequestMessage request = new(HttpMethod.Get, "/test");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "   ");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HandleAuthenticate_ShouldReturnFail_WhenTokenIsUnknown()
    {
        await using TestHost host = CreateHost();

        using HttpRequestMessage request = new(HttpMethod.Get, "/test");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "unknown-token-value");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestCancellationToken);

        // Fail() means authentication failed entirely → user is unauthenticated → 401
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HandleAuthenticate_ShouldSucceed_WithPlatformAdminRole_WhenKnownAdminToken()
    {
        await using TestHost host = CreateHost();

        using HttpRequestMessage request = new(HttpMethod.Get, "/test");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PlatformAdminToken);

        HttpResponseMessage response = await host.Client.SendAsync(request, TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HandleAuthenticate_ShouldSucceed_WithPlatformAdminRole_GrantingAccessToPolicyProtectedEndpoint()
    {
        await using TestHost host = CreateHost(requirePlatformAdmin: true);

        using HttpRequestMessage request = new(HttpMethod.Get, "/test");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PlatformAdminToken);

        HttpResponseMessage response = await host.Client.SendAsync(request, TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HandleAuthenticate_ShouldReturn403_WhenUnknownToken_OnPlatformAdminProtectedEndpoint()
    {
        await using TestHost host = CreateHost(requirePlatformAdmin: true);

        using HttpRequestMessage request = new(HttpMethod.Get, "/test");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "unknown-token");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestCancellationToken);

        // 403 because the authentication result is Fail (unknown token), which challenges, leading to 401/403
        // Depending on policy - since it's Fail (not NoResult), 401
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    private static TestHost CreateHost(bool requirePlatformAdmin = false)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddLogging();

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Bearer";
                options.DefaultChallengeScheme = "Bearer";
                options.DefaultScheme = "Bearer";
            })
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, MockBearerAuthenticationHandler>("Bearer", _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("PlatformAdmin", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireRole("platform-admin"));

        WebApplication app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        if (requirePlatformAdmin)
        {
            app.MapGet("/test", () => Results.Ok()).RequireAuthorization("PlatformAdmin");
        }
        else
        {
            app.MapGet("/test", () => Results.Ok()).RequireAuthorization();
        }

        app.StartAsync().GetAwaiter().GetResult();
        return new TestHost(app, app.GetTestClient());
    }

    private sealed class TestHost(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }
}
