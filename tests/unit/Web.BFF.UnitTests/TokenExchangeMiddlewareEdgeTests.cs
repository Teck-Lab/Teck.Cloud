using Microsoft.AspNetCore.Http;
using Xunit;
using NSubstitute;
using Shouldly;
using Web.BFF.Middleware;
using Web.BFF.Services;
using Yarp.ReverseProxy.Configuration;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Web.BFF.UnitTests;

public class TokenExchangeMiddlewareEdgeTests
{
    [Fact]
    public async Task Middleware_NoAuthorization_DoesNotCallExchange()
    {
        var exchange = Substitute.For<ITokenExchangeService>();
n        var middleware = new TokenExchangeMiddleware(async (ctx) => { await ctx.Response.WriteAsync("ok"); }, exchange);
        var ctx = new DefaultHttpContext();
        // Provide endpoint with audience but no auth header
        var route = new RouteConfig { Metadata = new Dictionary<string, string> { ["KeycloakAudience"] = "aud" } }; 
        var endpoint = new Endpoint((c) => Task.CompletedTask, new EndpointMetadataCollection(route), "route");
        ctx.SetEndpoint(endpoint);
        await middleware.InvokeAsync(ctx);
        exchange.DidNotReceiveWithAnyArgs().ExchangeTokenAsync(default!, default!, default!, default);
    }
    [Fact]
    public async Task Middleware_NoAudience_DoesNotCallExchange()
    {
        var exchange = Substitute.For<ITokenExchangeService>();
        var middleware = new TokenExchangeMiddleware(async (ctx) => { await ctx.Response.WriteAsync("ok"); }, exchange);
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Authorization"] = "Bearer subj";
        // Endpoint without route metadata
        var endpoint = new Endpoint((c) => Task.CompletedTask, new EndpointMetadataCollection(), "route");        ctx.SetEndpoint(endpoint);
        await middleware.InvokeAsync(ctx);
        exchange.DidNotReceiveWithAnyArgs().ExchangeTokenAsync(default!, default!, default!, default);
    }
}
