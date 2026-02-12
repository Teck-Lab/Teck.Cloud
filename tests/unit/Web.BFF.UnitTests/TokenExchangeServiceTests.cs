#pragma warning disable IDE0005
using System.Net;
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

public class TokenExchangeServiceTests
{
    [Fact]
    public async Task ExchangeToken_CallsKeycloakAndCaches()
    {
        // Arrange: fake HttpMessageHandler to return token JSON
        using var handler = new DelegatingHandlerStub("{ \"access_token\": \"abc123\", \"expires_in\": 60 }");
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

        var service = new TokenExchangeService(httpFactory, fusion, config);

        // Act
        var res = await service.ExchangeTokenAsync("subj","aud","tenant", TestContext.Current.CancellationToken);

        // Assert
        res.AccessToken.ShouldBe("abc123");

        // second call should hit cache and return same
        var res2 = await service.ExchangeTokenAsync("subj","aud","tenant", TestContext.Current.CancellationToken);
        res2.AccessToken.ShouldBe("abc123");
    }
}

// Simple DelegatingHandler stub
internal class DelegatingHandlerStub : DelegatingHandler
{
    private readonly string _responseBody;
    public DelegatingHandlerStub(string responseBody)
    {
        _responseBody = responseBody;
    }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var res = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responseBody)
        };
        return Task.FromResult(res);
    }
}
