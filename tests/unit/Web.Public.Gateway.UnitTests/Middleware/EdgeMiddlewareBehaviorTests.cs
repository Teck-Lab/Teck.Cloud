using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.Infrastructure.Auth;
using SharedKernel.Infrastructure.MultiTenant;
using Shouldly;
using Web.Public.Gateway.Services;
using Yarp.ReverseProxy.Configuration;

namespace Web.Public.Gateway.UnitTests.Middleware;

public sealed class EdgeMiddlewareBehaviorTests
{
    [Fact]
    public async Task AdminMiddleware_ShouldReturn401_WhenEmployeeRouteAndUnauthenticated()
    {
        DefaultHttpContext context = CreateHttpContext(
            routeConfig: CreateRouteConfig(
                path: "/catalog/v1/items",
                metadata: new Dictionary<string, string>
                {
                    ["EdgeAccessPolicy"] = "EmployeeOnly",
                }),
            requestPath: "/catalog/v1/items",
            authenticationService: new AlwaysFailAuthenticationService());

        RequestDelegate next = _ => throw new InvalidOperationException("Next should not be called for 401 response.");
        object middleware = CreateAdminMiddleware(next);

        await InvokeMiddlewareAsync(middleware, context);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task AdminMiddleware_ShouldReturn403_WhenEmployeeRouteAndRoleMissing()
    {
        DefaultHttpContext context = CreateHttpContext(
            routeConfig: CreateRouteConfig(
                path: "/catalog/v1/items",
                metadata: new Dictionary<string, string>
                {
                    ["EdgeAccessPolicy"] = "EmployeeOnly",
                }),
            requestPath: "/catalog/v1/items",
            authenticationService: new AlwaysFailAuthenticationService());

        context.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(authenticationType: "test-auth"));

        RequestDelegate next = _ => throw new InvalidOperationException("Next should not be called for 403 response.");
        object middleware = CreateAdminMiddleware(next);

        await InvokeMiddlewareAsync(middleware, context);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task AdminMiddleware_ShouldCallNext_WhenEmployeeRolePresent()
    {
        DefaultHttpContext context = CreateHttpContext(
            routeConfig: CreateRouteConfig(
                path: "/catalog/v1/items",
                metadata: new Dictionary<string, string>
                {
                    ["EdgeAccessPolicy"] = "EmployeeOnly",
                }),
            requestPath: "/catalog/v1/items",
            authenticationService: new AlwaysFailAuthenticationService());

        context.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim("role", "employee"),
            ],
            authenticationType: "test-auth"));

        bool nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        object middleware = CreateAdminMiddleware(next);

        await InvokeMiddlewareAsync(middleware, context);

        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task TenantMiddleware_ShouldReturn400_WhenPublicRouteMissingTenantContext()
    {
        RequestDelegate next = _ => throw new InvalidOperationException("Next should not be called for 400 response.");
        object middleware = CreateTenantMiddleware(next);

        DefaultHttpContext context = CreateHttpContext(
            routeConfig: CreateRouteConfig(path: "/catalog/v1/public", authorizationPolicy: "anonymous"),
            requestPath: "/catalog/v1/public",
            authenticationService: new AlwaysFailAuthenticationService());

        await InvokeMiddlewareAsync(middleware, context);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task TenantMiddleware_ShouldCallNext_WhenDocsRequest()
    {
        bool nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        object middleware = CreateTenantMiddleware(next);

        DefaultHttpContext context = CreateHttpContext(
            routeConfig: CreateRouteConfig(path: "/catalog/v1/items"),
            requestPath: "/docs",
            authenticationService: new AlwaysFailAuthenticationService());

        await InvokeMiddlewareAsync(middleware, context);

        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task TenantMiddleware_ShouldCallNext_WhenPublicOpenApiRequest()
    {
        bool nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        object middleware = CreateTenantMiddleware(next);

        DefaultHttpContext context = CreateHttpContext(
            routeConfig: CreateRouteConfig(path: "/catalog/v1/items"),
            requestPath: "/catalog/openapi/v1-public/openapi.json",
            authenticationService: new AlwaysFailAuthenticationService());

        await InvokeMiddlewareAsync(middleware, context);

        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task TenantMiddleware_ShouldReturn401_WhenTenantPolicyRequiredOnAdminPathAndUnauthenticated()
    {
        RequestDelegate next = _ => throw new InvalidOperationException("Next should not be called for 401 response.");
        object middleware = CreateTenantMiddleware(next);

        DefaultHttpContext context = CreateHttpContext(
            routeConfig: CreateRouteConfig(
                path: "/catalog/admin/items",
                metadata: new Dictionary<string, string>
                {
                    ["EdgeTenantPolicy"] = "Required",
                }),
            requestPath: "/catalog/admin/items",
            authenticationService: new AlwaysFailAuthenticationService());

        await InvokeMiddlewareAsync(middleware, context);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task TenantMiddleware_ShouldReturn403_WhenTenantHeaderDoesNotMatchTokenTenants()
    {
        RequestDelegate next = _ => throw new InvalidOperationException("Next should not be called for 403 response.");
        object middleware = CreateTenantMiddleware(next);

        DefaultHttpContext context = CreateHttpContext(
            routeConfig: CreateRouteConfig(path: "/catalog/v1/items"),
            requestPath: "/catalog/v1/items",
            authenticationService: new AlwaysFailAuthenticationService());

        context.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim("organization", "{\"tenant-a\":{\"id\":\"tenant-a\"}}"),
            ],
            authenticationType: "test-auth"));

        context.Request.Headers["X-TenantId"] = "tenant-b";

        await InvokeMiddlewareAsync(middleware, context);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task TenantMiddleware_ShouldCallNext_AfterTenantResolutionAndLookupSucceed_OnNonAdminRoute()
    {
        bool nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        object middleware = CreateTenantMiddleware(next);

        DefaultHttpContext context = CreateHttpContext(
            routeConfig: CreateRouteConfig(path: "/catalog/v1/items"),
            requestPath: "/catalog/v1/items",
            authenticationService: new AlwaysFailAuthenticationService());

        context.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim("organization", "{\"tenant-a\":{\"id\":\"tenant-a\"}}"),
            ],
            authenticationType: "test-auth"));

        context.Request.Headers["X-TenantId"] = "tenant-a";

        await InvokeMiddlewareAsync(middleware, context);

        nextCalled.ShouldBeTrue();
        context.Request.Headers["X-TenantId"].ToString().ShouldBe("tenant-a");
        context.Request.Headers["X-Tenant-DbStrategy"].ToString().ShouldBe("Shared");
    }

    [Fact]
    public async Task TenantMiddleware_ShouldNotAttemptLookup_WhenTenantResolutionFails_OnNonAdminRoute()
    {
        RequestDelegate next = _ => throw new InvalidOperationException("Next should not be called when tenant resolution fails.");
        object middleware = CreateTenantMiddleware(next);

        DefaultHttpContext context = CreateHttpContext(
            routeConfig: CreateRouteConfig(path: "/catalog/v1/items"),
            requestPath: "/catalog/v1/items",
            authenticationService: new AlwaysFailAuthenticationService());

        context.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim("organization", "{\"tenant-a\":{\"id\":\"tenant-a\"}}"),
            ],
            authenticationType: "test-auth"));

        context.Request.Headers["X-TenantId"] = "tenant-b";

        await InvokeMiddlewareAsync(middleware, context);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    private static async Task InvokeMiddlewareAsync(object middleware, HttpContext context)
    {
        Task invocation = (Task)middleware.GetType()
            .GetMethod("InvokeAsync")!
            .Invoke(middleware, [context])!;

        await invocation.ConfigureAwait(false);
    }

    private static DefaultHttpContext CreateHttpContext(RouteConfig routeConfig, string requestPath, IAuthenticationService authenticationService)
    {
        var services = new ServiceCollection();
        services.AddSingleton(authenticationService);

        var serviceProvider = services.BuildServiceProvider();

        DefaultHttpContext context = new()
        {
            RequestServices = serviceProvider,
        };

        context.Request.Path = requestPath;

        Endpoint endpoint = new(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(routeConfig),
            "test-route");

        context.SetEndpoint(endpoint);

        return context;
    }

    private static RouteConfig CreateRouteConfig(string path, string? authorizationPolicy = null, Dictionary<string, string>? metadata = null)
    {
        return new RouteConfig
        {
            RouteId = "test",
            ClusterId = "catalog",
            AuthorizationPolicy = authorizationPolicy,
            Match = new RouteMatch
            {
                Path = path,
            },
            Metadata = metadata,
        };
    }

    private static object CreateEdgeRouteSecurityOptions(bool enabled, string adminPathSegment, string employeeRole)
    {
        Type optionsType = GetTypeByName("Web.Public.Gateway.Services.EdgeRouteSecurityOptions");
        return Activator.CreateInstance(optionsType, enabled, adminPathSegment, employeeRole)!;
    }

    private static object CreateEdgeTenantOptions(string tenantHeader, string organizationClaimName, string tenantIdClaimName)
    {
        Type optionsType = GetTypeByName("Web.Public.Gateway.Services.EdgeTenantOptions");
        return Activator.CreateInstance(optionsType, tenantHeader, organizationClaimName, tenantIdClaimName)!;
    }

    private static object CreateTypedLoggerFor(Type targetType)
    {
        Type loggerType = typeof(TestLogger<>).MakeGenericType(targetType);
        return Activator.CreateInstance(loggerType)!;
    }

    private static object CreateAdminMiddleware(RequestDelegate next)
    {
        Type middlewareType = GetTypeByName("Web.Public.Gateway.Middleware.AdminRouteAuthorizationMiddleware");
        object securityOptions = CreateEdgeRouteSecurityOptions(enabled: true, adminPathSegment: "admin", employeeRole: "employee");
        return Activator.CreateInstance(middlewareType, next, securityOptions)!;
    }

    private static object CreateTenantMiddleware(RequestDelegate next)
    {
        Type middlewareType = GetTypeByName("Web.Public.Gateway.Middleware.TenantEnforcementMiddleware");
        object tenantOptions = CreateEdgeTenantOptions("X-TenantId", "organization", "tenant_id");
        object securityOptions = CreateEdgeRouteSecurityOptions(enabled: true, adminPathSegment: "admin", employeeRole: "employee");
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        object logger = CreateTypedLoggerFor(middlewareType);

        return Activator.CreateInstance(
            middlewareType,
            next,
            tenantOptions,
            securityOptions,
            new TenantTokenContextResolver(),
            new NoOpTokenExchangeService(),
            new SuccessTenantDatabaseStrategyResolver(),
            configuration,
            logger)!;
    }

    private static Type GetTypeByName(string fullName)
    {
        return Type.GetType($"{fullName}, Web.Public.Gateway", throwOnError: true)!;
    }

    private sealed class SuccessTenantDatabaseStrategyResolver : ITenantDatabaseStrategyResolver
    {
        public Task<TenantDatabaseStrategyLookupResult> ResolveAsync(string tenantId, string? serviceName, CancellationToken cancellationToken)
        {
            _ = tenantId;
            _ = serviceName;
            _ = cancellationToken;
            return Task.FromResult(new TenantDatabaseStrategyLookupResult(true, "Shared", null, null, null));
        }
    }

    private sealed class NoOpTokenExchangeService : IServiceTokenExchangeService
    {
        public Task<ServiceTokenResult> ExchangeTokenAsync(
            string subjectToken,
            string audience,
            string contextKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ServiceTokenResult("token", DateTime.UtcNow.AddMinutes(5)));
        }
    }

    private sealed class AlwaysFailAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            return Task.FromResult(AuthenticateResult.Fail("Unauthenticated"));
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task SignInAsync(HttpContext context, string? scheme, System.Security.Claims.ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        IDisposable? ILogger.BeginScope<TState>(TState state)
        {
            return null;
        }

        bool ILogger.IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        void ILogger.Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}
