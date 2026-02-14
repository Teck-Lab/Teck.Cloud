using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Web.Edge.Middleware;
using Web.Edge.Security;

namespace Web.Edge.UnitTests;

public sealed class EdgeRequestSanitizationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_RemovesSpoofableHeaders_AndAddsInternalIdentityHeader()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EdgeTrust:SigningKey"] = "0123456789abcdef0123456789abcdef",
                ["EdgeTrust:Issuer"] = "teck-edge",
                ["EdgeTrust:Audience"] = "teck-web-bff-internal",
            })
            .Build();

        var tokenService = new InternalIdentityTokenService(configuration, NullLogger<InternalIdentityTokenService>.Instance);

        var middleware = new EdgeRequestSanitizationMiddleware(
            _ => Task.CompletedTask,
            tokenService);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-TenantId"] = "spoofed";
        context.Request.Headers["X-Tenant-DbStrategy"] = "Dedicated";
        context.Request.Headers["X-Forwarded-User"] = "spoofed-user";

        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "user-1"),
            new Claim("tenant_id", "tenant-a"),
        ], "test-auth"));

        await middleware.InvokeAsync(context);

        context.Request.Headers.ContainsKey("X-TenantId").ShouldBeFalse();
        context.Request.Headers.ContainsKey("X-Tenant-DbStrategy").ShouldBeFalse();
        context.Request.Headers.ContainsKey("X-Forwarded-User").ShouldBeFalse();

        context.Request.Headers.TryGetValue("X-Internal-Identity", out var internalIdentity).ShouldBeTrue();
        internalIdentity.ToString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InvokeAsync_DoesNotAddInternalIdentity_WhenUserIsAnonymous()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EdgeTrust:SigningKey"] = "0123456789abcdef0123456789abcdef",
            })
            .Build();

        var tokenService = new InternalIdentityTokenService(configuration, NullLogger<InternalIdentityTokenService>.Instance);

        var middleware = new EdgeRequestSanitizationMiddleware(
            _ => Task.CompletedTask,
            tokenService);

        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        context.Request.Headers.ContainsKey("X-Internal-Identity").ShouldBeFalse();
    }
}
