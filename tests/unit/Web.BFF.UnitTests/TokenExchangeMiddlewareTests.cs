using Microsoft.AspNetCore.Http;
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
        exchange.ExchangeTokenAsync("subj","aud",Arg.Any<string>(),Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TokenResult("exchanged-token", DateTime.UtcNow.AddMinutes(1))));

        var middleware = new TokenExchangeMiddleware(async (ctx) =>
        {
            // terminal delegate
            await ctx.Response.WriteAsync("ok");
        }, exchange);

        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Authorization"] = "Bearer subj";

        // Provide an endpoint with RouteConfig metadata containing KeycloakAudience
        var route = new Yarp.ReverseProxy.Configuration.RouteConfig { Metadata = new Dictionary<string, string> { ["KeycloakAudience"] = "aud" } };
        var endpoint = new Endpoint((c) => Task.CompletedTask, new EndpointMetadataCollection(route), "route");
        ctx.SetEndpoint(endpoint);

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert
        ctx.Request.Headers["Authorization"].ToString().ShouldBe("Bearer exchanged-token");
    }
}
