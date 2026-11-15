using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Infrastructure.MultiTenancy;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.UnitTests;

/// <summary>Platform access selects a tenant by route only after authorization; no tenant header grants access.</summary>
public sealed class AdministrativeTenantTests
{
    /// <summary>A privileged actor can target an active tenant without membership; other actors are rejected.</summary>
    /// <param name="authorized">Whether the actor has the explicit platform permission.</param>
    /// <param name="active">Whether the target organization is active.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RouteSelectionRequiresExplicitPermission(bool authorized, bool active)
    {
        const string tenantId = "153fcc9a-7cfb-4283-908b-54ae814140f8";
        const string policyName = "OperateTenant";
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddAuthorizationBuilder().AddPolicy(policyName, policy => policy.RequireAuthenticatedUser().RequireClaim("platform_permission", "tenants:operate"));
        var directory = Substitute.For<ITenantDirectory>();
        _ = directory.IsActiveAsync(tenantId, Arg.Any<CancellationToken>()).Returns(active);
        _ = services.AddSingleton(directory);
        await using var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        var claims = new List<Claim> { new("sub", "platform-actor") };
        if (authorized)
        {
            claims.Add(new Claim("platform_permission", "tenants:operate"));
        }

        http.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "unit-test"));
        http.Request.RouteValues["tenantId"] = tenantId.ToUpperInvariant();
        http.Request.Headers["X-Tenant-Id"] = "untrusted-other-tenant";
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(OperationScope.AdministrativeTenant("tenantId", policyName)), "admin-tenant"));
        var operation = new OperationContext();
        var called = false;
        var middleware = new OperationScopeMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(http, operation).ConfigureAwait(true);
        called.ShouldBe(authorized && active);
        if (called)
        {
            operation.Scope.TenantId.ShouldBe(tenantId);
            operation.ActorId.ShouldBe("platform-actor");
        }
        else
        {
            http.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
            _ = Should.Throw<InvalidOperationException>(() => operation.Scope);
        }

        _ = await directory.Received(authorized ? 1 : 0).IsActiveAsync(tenantId, Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    /// <summary>Missing route selection cannot silently become a global operation.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task MissingSelectionNeverCreatesGlobalScope()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddAuthorizationBuilder().AddPolicy("platform", policy => policy.RequireAuthenticatedUser());
        await using var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider, User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "admin")], "unit-test")) };
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(OperationScope.AdministrativeTenant("tenantId", "platform")), "admin"));
        var operation = new OperationContext();
        var middleware = new OperationScopeMiddleware(_ => throw new InvalidOperationException("Must not run."));
        await middleware.InvokeAsync(http, operation).ConfigureAwait(true);
        http.Response.StatusCode.ShouldBe(403);
        _ = Should.Throw<InvalidOperationException>(() => operation.Scope);
    }
}
