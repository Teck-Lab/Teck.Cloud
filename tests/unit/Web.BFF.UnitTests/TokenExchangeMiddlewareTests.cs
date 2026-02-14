using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Xunit;
using NSubstitute;
using Shouldly;
using Web.BFF.Middleware;
using Web.BFF.Services;

namespace Web.BFF.UnitTests;

public class TokenExchangeMiddlewareTests
{
    [Fact]
    public async Task Middleware_ExchangesTokenAndSetsHeaders()
    {
        // Arrange
        var exchange = Substitute.For<ITokenExchangeService>();
        var tenantRouting = Substitute.For<ITenantRoutingMetadataService>();
        exchange.ExchangeTokenAsync("subj","aud",Arg.Any<string>(),Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TokenResult("exchanged-token", DateTime.UtcNow.AddMinutes(1))));
        tenantRouting.GetTenantRoutingMetadataAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TenantRoutingMetadata?>(new TenantRoutingMetadata("tenant-a", "Shared")));

        var middleware = new TokenExchangeMiddleware(async (ctx) =>
        {
            // terminal delegate
            await ctx.Response.WriteAsync("ok");
        }, exchange, tenantRouting);

        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Authorization"] = "Bearer subj";
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenant_id", "tenant-a")
        ], "test"));

        // Provide an endpoint with RouteConfig metadata containing KeycloakAudience
        var route = new Yarp.ReverseProxy.Configuration.RouteConfig { Metadata = new Dictionary<string, string> { ["KeycloakAudience"] = "aud" } };
        var endpoint = new Endpoint((c) => Task.CompletedTask, new EndpointMetadataCollection(route), "route");
        ctx.SetEndpoint(endpoint);

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert
        ctx.Request.Headers["Authorization"].ToString().ShouldBe("Bearer exchanged-token");
        ctx.Request.Headers["X-TenantId"].ToString().ShouldBe("tenant-a");
        ctx.Request.Headers["X-Tenant-DbStrategy"].ToString().ShouldBe("Shared");
    }
}
