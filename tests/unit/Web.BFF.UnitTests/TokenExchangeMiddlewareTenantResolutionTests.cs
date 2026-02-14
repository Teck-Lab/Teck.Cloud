using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shouldly;
using System.Security.Claims;
using Web.BFF.Middleware;
using Web.BFF.Services;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace Web.BFF.UnitTests;

public class TokenExchangeMiddlewareTenantResolutionTests
{
    [Fact]
    public async Task Middleware_UsesOrganizationClaimId_ForTenantResolution()
    {
        var exchange = Substitute.For<ITokenExchangeService>();
        var tenantRouting = Substitute.For<ITenantRoutingMetadataService>();
        exchange.ExchangeTokenAsync("subj", "aud", "tenant-org", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TokenResult("exchanged-token", DateTime.UtcNow.AddMinutes(1))));

        var middleware = CreateMiddleware(exchange, tenantRouting);
        var ctx = CreateContext(
            "Bearer subj",
            new[]
            {
                new Claim("organization", "{\"acme\":{\"id\":\"tenant-org\"}}")
            },
            "aud");

        await middleware.InvokeAsync(ctx);

        ctx.Request.Headers["X-TenantId"].ToString().ShouldBe("tenant-org");
        await exchange.Received(1).ExchangeTokenAsync("subj", "aud", "tenant-org", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Middleware_UsesActiveOrganizationFallback_WhenOrganizationClaimsMissing()
    {
        var exchange = Substitute.For<ITokenExchangeService>();
        var tenantRouting = Substitute.For<ITenantRoutingMetadataService>();
        exchange.ExchangeTokenAsync("subj", "aud", "tenant-active", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TokenResult("exchanged-token", DateTime.UtcNow.AddMinutes(1))));

        var middleware = CreateMiddleware(exchange, tenantRouting);
        var ctx = CreateContext(
            "Bearer subj",
            new[]
            {
                new Claim("active_organization", "{\"id\":\"tenant-active\"}")
            },
            "aud");

        await middleware.InvokeAsync(ctx);

        ctx.Request.Headers["X-TenantId"].ToString().ShouldBe("tenant-active");
        await exchange.Received(1).ExchangeTokenAsync("subj", "aud", "tenant-active", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Middleware_ContinuesWhenExchangeFails_AndStillSetsTenantHeaders()
    {
        var exchange = Substitute.For<ITokenExchangeService>();
        var tenantRouting = Substitute.For<ITenantRoutingMetadataService>();
        exchange.ExchangeTokenAsync("subj", "aud", "tenant-a", Arg.Any<CancellationToken>())
            .Returns<Task<TokenResult>>(_ => throw new Exception("boom"));
        tenantRouting.GetTenantRoutingMetadataAsync("tenant-a", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TenantRoutingMetadata?>(new TenantRoutingMetadata("tenant-a", "Shared")));

        var middleware = CreateMiddleware(exchange, tenantRouting);
        var ctx = CreateContext(
            "Bearer subj",
            new[]
            {
                new Claim("tenant_id", "tenant-a")
            },
            "aud");

        await middleware.InvokeAsync(ctx);

        ctx.Request.Headers["Authorization"].ToString().ShouldBe("Bearer subj");
        ctx.Request.Headers["X-TenantId"].ToString().ShouldBe("tenant-a");
        ctx.Request.Headers["X-Tenant-DbStrategy"].ToString().ShouldBe("Shared");
    }

    [Fact]
    public async Task Middleware_NoTenant_DoesNotCallTenantRoutingMetadataService()
    {
        var exchange = Substitute.For<ITokenExchangeService>();
        var tenantRouting = Substitute.For<ITenantRoutingMetadataService>();
        exchange.ExchangeTokenAsync("subj", "aud", "", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TokenResult("exchanged-token", DateTime.UtcNow.AddMinutes(1))));

        var middleware = CreateMiddleware(exchange, tenantRouting);
        var ctx = CreateContext("Bearer subj", Array.Empty<Claim>(), "aud");

        await middleware.InvokeAsync(ctx);

        await tenantRouting.DidNotReceiveWithAnyArgs().GetTenantRoutingMetadataAsync(default!, TestContext.Current.CancellationToken);
        ctx.Request.Headers.ContainsKey("X-TenantId").ShouldBeFalse();
    }

    [Fact]
    public async Task Middleware_UsesOrganizationsClaimStringValue_AsTenantId()
    {
        var exchange = Substitute.For<ITokenExchangeService>();
        var tenantRouting = Substitute.For<ITenantRoutingMetadataService>();
        exchange.ExchangeTokenAsync("subj", "aud", "tenant-value", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TokenResult("exchanged-token", DateTime.UtcNow.AddMinutes(1))));

        var middleware = CreateMiddleware(exchange, tenantRouting);
        var ctx = CreateContext(
            "Bearer subj",
            new[]
            {
                new Claim("organizations", "{\"acme\":\"tenant-value\"}")
            },
            "aud");

        await middleware.InvokeAsync(ctx);

        ctx.Request.Headers["X-TenantId"].ToString().ShouldBe("tenant-value");
        await exchange.Received(1).ExchangeTokenAsync("subj", "aud", "tenant-value", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Middleware_UsesOrganizationsClaimObjectKey_WhenIdMissing()
    {
        var exchange = Substitute.For<ITokenExchangeService>();
        var tenantRouting = Substitute.For<ITenantRoutingMetadataService>();
        exchange.ExchangeTokenAsync("subj", "aud", "acme", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TokenResult("exchanged-token", DateTime.UtcNow.AddMinutes(1))));

        var middleware = CreateMiddleware(exchange, tenantRouting);
        var ctx = CreateContext(
            "Bearer subj",
            new[]
            {
                new Claim("organizations", "{\"acme\":{\"attr\":[\"x\"]}}")
            },
            "aud");

        await middleware.InvokeAsync(ctx);

        ctx.Request.Headers["X-TenantId"].ToString().ShouldBe("acme");
        await exchange.Received(1).ExchangeTokenAsync("subj", "aud", "acme", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Middleware_MalformedOrganizationClaim_FallsBackToTenantIdClaim()
    {
        var exchange = Substitute.For<ITokenExchangeService>();
        var tenantRouting = Substitute.For<ITenantRoutingMetadataService>();
        exchange.ExchangeTokenAsync("subj", "aud", "tenant-fallback", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TokenResult("exchanged-token", DateTime.UtcNow.AddMinutes(1))));

        var middleware = CreateMiddleware(exchange, tenantRouting);
        var ctx = CreateContext(
            "Bearer subj",
            new[]
            {
                new Claim("organization", "{not-json"),
                new Claim("tenant_id", "tenant-fallback")
            },
            "aud");

        await middleware.InvokeAsync(ctx);

        ctx.Request.Headers["X-TenantId"].ToString().ShouldBe("tenant-fallback");
        await exchange.Received(1).ExchangeTokenAsync("subj", "aud", "tenant-fallback", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Middleware_NoAudience_StillSetsTenantHeader()
    {
        var exchange = Substitute.For<ITokenExchangeService>();
        var tenantRouting = Substitute.For<ITenantRoutingMetadataService>();

        var middleware = CreateMiddleware(exchange, tenantRouting);
        var ctx = CreateContext(
            "Bearer subj",
            new[]
            {
                new Claim("tenant_id", "tenant-a")
            },
            audience: null);

        await middleware.InvokeAsync(ctx);

        ctx.Request.Headers["X-TenantId"].ToString().ShouldBe("tenant-a");
        await exchange.DidNotReceiveWithAnyArgs().ExchangeTokenAsync(default!, default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Middleware_TenantMetadataLookupFails_StillContinuesWithTenantHeader()
    {
        var exchange = Substitute.For<ITokenExchangeService>();
        var tenantRouting = Substitute.For<ITenantRoutingMetadataService>();
        exchange.ExchangeTokenAsync("subj", "aud", "tenant-a", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TokenResult("exchanged-token", DateTime.UtcNow.AddMinutes(1))));
        tenantRouting.GetTenantRoutingMetadataAsync("tenant-a", Arg.Any<CancellationToken>())
            .Returns<Task<TenantRoutingMetadata?>>(_ => throw new Exception("metadata unavailable"));

        var middleware = CreateMiddleware(exchange, tenantRouting);
        var ctx = CreateContext(
            "Bearer subj",
            new[]
            {
                new Claim("tenant_id", "tenant-a")
            },
            "aud");

        await middleware.InvokeAsync(ctx);

        ctx.Request.Headers["Authorization"].ToString().ShouldBe("Bearer exchanged-token");
        ctx.Request.Headers["X-TenantId"].ToString().ShouldBe("tenant-a");
        ctx.Request.Headers.ContainsKey("X-Tenant-DbStrategy").ShouldBeFalse();
    }

    private static TokenExchangeMiddleware CreateMiddleware(
        ITokenExchangeService exchange,
        ITenantRoutingMetadataService tenantRouting)
    {
        return new TokenExchangeMiddleware(
            async context => await context.Response.WriteAsync("ok"),
            exchange,
            tenantRouting);
    }

    private static DefaultHttpContext CreateContext(
        string? authorization,
        IEnumerable<Claim> claims,
        string? audience)
    {
        var ctx = new DefaultHttpContext();
        if (!string.IsNullOrWhiteSpace(authorization))
        {
            ctx.Request.Headers["Authorization"] = authorization;
        }

        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        var metadata = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(audience))
        {
            metadata["KeycloakAudience"] = audience;
        }

        var route = new RouteConfig { Metadata = metadata };
        var endpoint = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(route), "route");
        ctx.SetEndpoint(endpoint);

        return ctx;
    }
}
