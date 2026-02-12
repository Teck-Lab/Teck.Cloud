using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Xunit;
using NSubstitute;
using Shouldly;
using ZiggyCreatures.Caching.Fusion;
using Web.BFF.Services;

namespace Web.BFF.UnitTests;

public class TokenExchangeServiceEdgeTests
{
    [Fact]
    public async Task ExchangeToken_ThrowsOnMissingSubjectOrAudience()
    {
        using var fusion = new FusionCache(new FusionCacheOptions());
        var httpFactory = Substitute.For<IHttpClientFactory>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>()
        {
            ["Keycloak:Authority"] = "https://example.com",
            ["Keycloak:GatewayClientId"] = "gateway",
            ["Keycloak:GatewayClientSecret"] = "secret"
        }).Build();

        var svc = new TokenExchangeService(httpFactory, fusion, config);

        await Should.ThrowAsync<ArgumentNullException>(async () => await svc.ExchangeTokenAsync(null!, "aud", "t"));
        await Should.ThrowAsync<ArgumentNullException>(async () => await svc.ExchangeTokenAsync("subj", null!, "t"));
    }

    [Fact]
    public async Task ExchangeToken_PropagatesHttpErrors()
    {
        // Arrange: handler returns 500
        using var handler = new DelegatingHandlerStub("{ \"error\": \"bad\" }", HttpStatusCode.InternalServerError);
        using var client = new HttpClient(handler);
        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient(Arg.Is<string>(s => s == "KeycloakTokenClient")).Returns(client);

        using var fusion = new FusionCache(new FusionCacheOptions());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>()
        {
            ["Keycloak:Authority"] = "https://example.com",
            ["Keycloak:GatewayClientId"] = "gateway",
            ["Keycloak:GatewayClientSecret"] = "secret"
        }).Build();

        var svc = new TokenExchangeService(httpFactory, fusion, config);

        await Should.ThrowAsync<HttpRequestException>(async () => await svc.ExchangeTokenAsync("subj","aud","t"));
    }
}
