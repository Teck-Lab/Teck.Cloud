using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using SharedKernel.Infrastructure.Auth;
using SharedKernel.Infrastructure.MultiTenant;
using Web.Edge.Services;
using Yarp.ReverseProxy.Configuration;
#pragma warning disable CS0618
#pragma warning disable CA2007

namespace Web.Edge.IntegrationTests.Middleware;

public sealed class EdgeGatewayFlowIntegrationTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EmployeeOnlyRoute_ShouldReturn401_WhenUnauthenticated()
    {
        RouteConfig routeConfig = CreateRouteConfig(
            path: "/customer/v1/Tenants",
            metadata: new Dictionary<string, string>
            {
                ["EdgeAccessPolicy"] = "EmployeeOnly",
                ["EdgeTenantPolicy"] = "None",
            });

        await using TestHostApp host = await CreateHostAsync(routeConfig, GrpcLookupMode.Success);

        HttpResponseMessage response = await host.Client.GetAsync(new Uri("/customer/v1/Tenants", UriKind.Relative), TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EmployeeOnlyRoute_ShouldReturn403_WhenEmployeeRoleMissing()
    {
        RouteConfig routeConfig = CreateRouteConfig(
            path: "/customer/v1/Tenants",
            metadata: new Dictionary<string, string>
            {
                ["EdgeAccessPolicy"] = "EmployeeOnly",
                ["EdgeTenantPolicy"] = "None",
            });

        await using TestHostApp host = await CreateHostAsync(routeConfig, GrpcLookupMode.Success);

        using HttpRequestMessage request = new(HttpMethod.Get, "/customer/v1/Tenants");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PublicRoute_ShouldReturn400_WhenTenantHeaderAndTokenTenantMissing()
    {
        RouteConfig routeConfig = CreateRouteConfig(
            path: "/catalog/v1/public",
            authorizationPolicy: "anonymous");

        await using TestHostApp host = await CreateHostAsync(routeConfig, GrpcLookupMode.Success);

        HttpResponseMessage response = await host.Client.GetAsync(new Uri("/catalog/v1/public", UriKind.Relative), TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProtectedRoute_ShouldReturn403_WhenTenantHeaderDoesNotMatchTokenTenant()
    {
        RouteConfig routeConfig = CreateRouteConfig(path: "/catalog/v1/items");

        await using TestHostApp host = await CreateHostAsync(routeConfig, GrpcLookupMode.Success);

        using HttpRequestMessage request = new(HttpMethod.Get, "/catalog/v1/items");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        request.Headers.Add("X-Test-Organization", "{\"tenant-a\":{\"id\":\"tenant-a\"}}");
        request.Headers.Add("X-TenantId", "tenant-b");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProtectedRoute_ShouldReturn404_WhenTenantLookupReturnsNotFound()
    {
        RouteConfig routeConfig = CreateRouteConfig(path: "/catalog/v1/items");

        await using TestHostApp host = await CreateHostAsync(routeConfig, GrpcLookupMode.NotFound);

        using HttpRequestMessage request = new(HttpMethod.Get, "/catalog/v1/items");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        request.Headers.Add("X-Test-Organization", "{\"tenant-a\":{\"id\":\"tenant-a\"}}");
        request.Headers.Add("X-TenantId", "tenant-a");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ProtectedRoute_ShouldReturn503_WhenTenantLookupUnavailable()
    {
        RouteConfig routeConfig = CreateRouteConfig(path: "/catalog/v1/items");

        await using TestHostApp host = await CreateHostAsync(routeConfig, GrpcLookupMode.Unavailable);

        using HttpRequestMessage request = new(HttpMethod.Get, "/catalog/v1/items");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        request.Headers.Add("X-Test-Organization", "{\"tenant-a\":{\"id\":\"tenant-a\"}}");
        request.Headers.Add("X-TenantId", "tenant-a");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task ProtectedRoute_ShouldReturn200_AndForwardTenantHeaders_WhenLookupSucceeds()
    {
        RouteConfig routeConfig = CreateRouteConfig(path: "/catalog/v1/items");

        await using TestHostApp host = await CreateHostAsync(routeConfig, GrpcLookupMode.Success);

        using HttpRequestMessage request = new(HttpMethod.Get, "/catalog/v1/items");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        request.Headers.Add("X-Test-Organization", "{\"tenant-a\":{\"id\":\"tenant-a\"}}");
        request.Headers.Add("X-TenantId", "tenant-a");

        HttpResponseMessage response = await host.Client.SendAsync(request, TestCancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        string payload = await response.Content.ReadAsStringAsync(TestCancellationToken);
        using JsonDocument json = JsonDocument.Parse(payload);
        json.RootElement.GetProperty("tenantId").GetString().ShouldBe("tenant-a");
        json.RootElement.GetProperty("tenantDbStrategy").GetString().ShouldBe("Shared");
        json.RootElement.GetProperty("hasExchangedToken").GetBoolean().ShouldBeTrue();
    }

    private static RouteConfig CreateRouteConfig(
        string path,
        string? authorizationPolicy = null,
        Dictionary<string, string>? metadata = null)
    {
        return new RouteConfig
        {
            RouteId = "integration-route",
            ClusterId = "catalog",
            AuthorizationPolicy = authorizationPolicy,
            Match = new RouteMatch { Path = path },
            Metadata = metadata,
        };
    }

    private static async Task<TestHostApp> CreateHostAsync(RouteConfig routeConfig, GrpcLookupMode lookupMode)
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

        builder.Services.AddAuthorization();

        Type edgeTenantOptionsType = ResolveType("Web.Edge.Services.EdgeTenantOptions");
        Type edgeRouteSecurityOptionsType = ResolveType("Web.Edge.Services.EdgeRouteSecurityOptions");

        object edgeTenantOptions = Activator.CreateInstance(edgeTenantOptionsType, "X-TenantId", "organization", "tenant_id")!;
        object edgeRouteSecurityOptions = Activator.CreateInstance(edgeRouteSecurityOptionsType, true, "admin", "employee")!;

        builder.Services.AddSingleton(edgeTenantOptionsType, edgeTenantOptions);
        builder.Services.AddSingleton(edgeRouteSecurityOptionsType, edgeRouteSecurityOptions);

        builder.Services.AddSingleton<ITenantTokenContextResolver, TenantTokenContextResolver>();
        builder.Services.AddSingleton<IServiceTokenExchangeService, FakeTokenExchangeService>();
        builder.Services.AddSingleton<ITenantDatabaseStrategyResolver>(new FakeTenantDatabaseStrategyResolver(lookupMode));

        WebApplication app = builder.Build();

        app.UseRouting();
        app.UseAuthentication();

        app.UseMiddleware(ResolveType("Web.Edge.Middleware.AdminRouteAuthorizationMiddleware"));
        app.UseMiddleware(ResolveType("Web.Edge.Middleware.TenantEnforcementMiddleware"));

        app.Map("/{**catch-all}", (HttpContext context) =>
        {
            return Results.Json(new
            {
                tenantId = context.Request.Headers["X-TenantId"].ToString(),
                tenantDbStrategy = context.Request.Headers["X-Tenant-DbStrategy"].ToString(),
                hasExchangedToken = context.Items.ContainsKey("Edge:ExchangedAccessToken"),
            });
        }).WithMetadata(routeConfig);

        await app.StartAsync(TestCancellationToken);

        return new TestHostApp(app, app.GetTestClient());
    }

    private static Type ResolveType(string fullName)
    {
        return Type.GetType($"{fullName}, Web.Edge", throwOnError: true)!;
    }

    private sealed class TestHostApp : IAsyncDisposable
    {
        private readonly WebApplication app;

        public TestHostApp(WebApplication app, HttpClient client)
        {
            this.app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }

    private enum GrpcLookupMode
    {
        Success,
        NotFound,
        Unavailable,
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Activated by DI service registration in integration host.")]
    private sealed class FakeTokenExchangeService : IServiceTokenExchangeService
    {
        public Task<ServiceTokenResult> ExchangeTokenAsync(
            string subjectToken,
            string audience,
            string contextKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ServiceTokenResult("exchanged-token", DateTime.UtcNow.AddMinutes(5)));
        }
    }

    private sealed class FakeTenantDatabaseStrategyResolver(GrpcLookupMode mode) : ITenantDatabaseStrategyResolver
    {
        private readonly GrpcLookupMode mode = mode;

        public Task<TenantDatabaseStrategyLookupResult> ResolveAsync(string tenantId, string? serviceName, CancellationToken cancellationToken)
        {
            _ = tenantId;
            _ = serviceName;
            _ = cancellationToken;

            if (mode == GrpcLookupMode.NotFound)
            {
                return Task.FromResult(new TenantDatabaseStrategyLookupResult(
                    false,
                    null,
                    StatusCodes.Status404NotFound,
                    "tenant.not_found",
                    "Tenant not found"));
            }

            if (mode == GrpcLookupMode.Unavailable)
            {
                return Task.FromResult(new TenantDatabaseStrategyLookupResult(
                    false,
                    null,
                    StatusCodes.Status503ServiceUnavailable,
                    "tenant.lookup.unavailable",
                    "Tenant lookup service is unavailable."));
            }

            return Task.FromResult(new TenantDatabaseStrategyLookupResult(true, "Shared", null, null, null));
        }
    }

    private sealed class TestAuthHandlerOptions : AuthenticationSchemeOptions
    {
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Activated by DI authentication scheme registration in integration host.")]
    private sealed class TestAuthHandler : AuthenticationHandler<TestAuthHandlerOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<TestAuthHandlerOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock)
            : base(options, logger, encoder, clock)
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
                new Claim(ClaimTypes.NameIdentifier, "integration-user"),
                new Claim(ClaimTypes.Name, "Integration User"),
            ];

            if (Request.Headers.TryGetValue("X-Test-Roles", out Microsoft.Extensions.Primitives.StringValues roleValues))
            {
                string[] roles = roleValues.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (string role in roles)
                {
                    claims.Add(new Claim("role", role));
                }
            }

            if (Request.Headers.TryGetValue("X-Test-Organization", out Microsoft.Extensions.Primitives.StringValues organizationValues))
            {
                claims.Add(new Claim("organization", organizationValues.ToString()));
            }

            if (Request.Headers.TryGetValue("X-Test-TenantIdClaim", out Microsoft.Extensions.Primitives.StringValues tenantClaimValues))
            {
                claims.Add(new Claim("tenant_id", tenantClaimValues.ToString()));
            }

            ClaimsIdentity identity = new(claims, "Bearer");
            ClaimsPrincipal principal = new(identity);
            AuthenticationTicket ticket = new(principal, "Bearer");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
#pragma warning restore CS0618
#pragma warning restore CA2007
