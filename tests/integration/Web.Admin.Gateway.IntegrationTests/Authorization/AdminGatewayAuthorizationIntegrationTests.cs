using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Yarp.ReverseProxy.Configuration;
#pragma warning disable CA2007

namespace Web.Admin.Gateway.IntegrationTests.Authorization;

/// <summary>
/// Integration tests for the admin gateway's PlatformAdmin authorization policy.
/// The admin gateway enforces that all proxied routes require the "platform-admin" realm role.
/// No tenant context is required (admins are cross-tenant).
/// </summary>
public sealed class AdminGatewayAuthorizationIntegrationTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AdminRoute_ShouldReturn401_WhenRequestIsUnauthenticated()
    {
        await using TestHostApp host = await CreateHostAsync(CreateAdminRouteConfig());

        HttpResponseMessage response = await host.Client.GetAsync(
            new Uri("/customer/v1/admin/Tenants", UriKind.Relative),
            TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminRoute_ShouldReturn403_WhenAuthenticatedButMissingPlatformAdminRole()
    {
        await using TestHostApp host = await CreateHostAsync(CreateAdminRouteConfig());

        using HttpRequestMessage request = new(HttpMethod.Get, "/customer/v1/admin/Tenants");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "valid-but-no-role");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminRoute_ShouldReturn200_WhenAuthenticatedWithPlatformAdminRole()
    {
        await using TestHostApp host = await CreateHostAsync(CreateAdminRouteConfig());

        using HttpRequestMessage request = new(HttpMethod.Get, "/customer/v1/admin/Tenants");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "valid-token");
        request.Headers.Add("X-Test-Roles", "platform-admin");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminRoute_ShouldReturn403_WhenAuthenticatedWithUnrelatedRole()
    {
        await using TestHostApp host = await CreateHostAsync(CreateAdminRouteConfig());

        using HttpRequestMessage request = new(HttpMethod.Get, "/customer/v1/admin/Tenants");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "valid-token");
        request.Headers.Add("X-Test-Roles", "employee");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminRoute_ShouldForwardBearerToken_WhenPlatformAdminAuthenticated()
    {
        await using TestHostApp host = await CreateHostAsync(CreateAdminRouteConfig());

        using HttpRequestMessage request = new(HttpMethod.Get, "/customer/v1/admin/Tenants");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "valid-token");
        request.Headers.Add("X-Test-Roles", "platform-admin");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string? forwardedAuth = response.Headers.TryGetValues("X-Echoed-Authorization", out var vals)
            ? vals.FirstOrDefault()
            : null;
        forwardedAuth.ShouldBe("Bearer valid-token");
    }

    [Fact]
    public async Task AdminRoute_ShouldNotRequireTenantHeader_WhenPlatformAdminAuthenticated()
    {
        await using TestHostApp host = await CreateHostAsync(CreateAdminRouteConfig());

        // No X-TenantId header — admin requests are cross-tenant
        using HttpRequestMessage request = new(HttpMethod.Get, "/customer/v1/admin/Tenants");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "valid-token");
        request.Headers.Add("X-Test-Roles", "platform-admin");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CatalogAdminRoute_ShouldReturn200_WhenPlatformAdmin()
    {
        RouteConfig route = new()
        {
            RouteId = "catalog-admin-route",
            ClusterId = "catalog",
            AuthorizationPolicy = "PlatformAdmin",
            Match = new RouteMatch { Path = "/catalog/v1/admin/{**catch-all}" },
        };

        await using TestHostApp host = await CreateHostAsync(route);

        using HttpRequestMessage request = new(HttpMethod.Get, "/catalog/v1/admin/products");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "valid-token");
        request.Headers.Add("X-Test-Roles", "platform-admin");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CatalogAdminRoute_ShouldReturn401_WhenUnauthenticated()
    {
        RouteConfig route = new()
        {
            RouteId = "catalog-admin-route",
            ClusterId = "catalog",
            AuthorizationPolicy = "PlatformAdmin",
            Match = new RouteMatch { Path = "/catalog/v1/admin/{**catch-all}" },
        };

        await using TestHostApp host = await CreateHostAsync(route);

        HttpResponseMessage response = await host.Client.GetAsync(
            new Uri("/catalog/v1/admin/products", UriKind.Relative),
            TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static RouteConfig CreateAdminRouteConfig()
    {
        return new RouteConfig
        {
            RouteId = "customer-admin-route",
            ClusterId = "customer",
            AuthorizationPolicy = "PlatformAdmin",
            Match = new RouteMatch { Path = "/customer/{**catch-all}" },
        };
    }

    private static async Task<TestHostApp> CreateHostAsync(RouteConfig routeConfig)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
        });

        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddLogging();

        builder.Services
            .AddAuthentication("Bearer")
            .AddScheme<TestAuthHandlerOptions, TestAuthHandler>("Bearer", _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("PlatformAdmin", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireRole("platform-admin"));

        WebApplication app = builder.Build();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.Map("/{**catch-all}", (HttpContext context) =>
        {
            // Echo the Authorization header so tests can verify it was forwarded
            string? authHeader = context.Request.Headers.Authorization.ToString();
            context.Response.Headers["X-Echoed-Authorization"] = authHeader;
            return Results.Ok();
        }).WithMetadata(routeConfig).RequireAuthorization("PlatformAdmin");

        await app.StartAsync();
        return new TestHostApp(app, app.GetTestClient());
    }

    private sealed class TestHostApp(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class TestAuthHandlerOptions : AuthenticationSchemeOptions
    {
    }

    private sealed class TestAuthHandler : AuthenticationHandler<TestAuthHandlerOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<TestAuthHandlerOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.Authorization.Count == 0)
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing bearer token."));
            }

            List<Claim> claims =
            [
                new Claim(ClaimTypes.NameIdentifier, "test-user"),
                new Claim(ClaimTypes.Name, "Test User"),
            ];

            if (Request.Headers.TryGetValue("X-Test-Roles", out var roleValues))
            {
                foreach (string role in roleValues.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }

            ClaimsIdentity identity = new(claims, "Bearer");
            ClaimsPrincipal principal = new(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "Bearer")));
        }
    }
}
#pragma warning restore CA2007
