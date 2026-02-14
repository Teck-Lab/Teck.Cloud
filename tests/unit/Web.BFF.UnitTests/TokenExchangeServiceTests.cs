#pragma warning disable IDE0005
using System.Net;
using System.Net.Http.Headers;
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

    [Fact]
    public async Task ExchangeToken_SendsExpectedTokenExchangePayload()
    {
        // Arrange
        using var handler = new DelegatingHandlerStub("{ \"access_token\": \"abc123\", \"expires_in\": 60 }");
        using var client = new HttpClient(handler);
        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient(Arg.Is<string>(s => s == "KeycloakTokenClient")).Returns(client);

        using var fusion = new FusionCache(new FusionCacheOptions());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()
        {
            ["Keycloak:Authority"] = "https://example.com",
            ["Keycloak:GatewayClientId"] = "gateway",
            ["Keycloak:GatewayClientSecret"] = "secret"
        }).Build();

        var service = new TokenExchangeService(httpFactory, fusion, config);

        // Act
        await service.ExchangeTokenAsync("subj", "aud", "tenant", TestContext.Current.CancellationToken);

        // Assert
        handler.LastRequestBody.ShouldNotBeNull();
        handler.LastRequestBody.ShouldContain("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Atoken-exchange");
        handler.LastRequestBody.ShouldContain("subject_token=subj");
        handler.LastRequestBody.ShouldContain("subject_token_type=urn%3Aietf%3Aparams%3Aoauth%3Atoken-type%3Aaccess_token");
        handler.LastRequestBody.ShouldContain("audience=aud");
    }

    [Fact]
    public async Task ExchangeToken_UsesConfiguredTokenEndpoint_WhenProvided()
    {
        // Arrange
        using var handler = new DelegatingHandlerStub("{ \"access_token\": \"abc123\", \"expires_in\": 60 }");
        using var client = new HttpClient(handler);
        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient(Arg.Is<string>(s => s == "KeycloakTokenClient")).Returns(client);

        using var fusion = new FusionCache(new FusionCacheOptions());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()
        {
            ["Keycloak:Authority"] = "https://example.com",
            ["Keycloak:TokenEndpoint"] = "https://example.com/custom-token",
            ["Keycloak:GatewayClientId"] = "gateway",
            ["Keycloak:GatewayClientSecret"] = "secret"
        }).Build();

        var service = new TokenExchangeService(httpFactory, fusion, config);

        // Act
        await service.ExchangeTokenAsync("subj", "aud", "tenant", TestContext.Current.CancellationToken);

        // Assert
        handler.LastRequestUri.ShouldBe(new Uri("https://example.com/custom-token"));
    }

    [Fact]
    public async Task ExchangeToken_CacheKeyIncludesSubjectAudienceAndTenant()
    {
        // Arrange
        using var handler = new DelegatingHandlerStub("{ \"access_token\": \"abc123\", \"expires_in\": 60 }");
        using var client = new HttpClient(handler);
        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient(Arg.Is<string>(s => s == "KeycloakTokenClient")).Returns(client);

        using var fusion = new FusionCache(new FusionCacheOptions());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()
        {
            ["Keycloak:Authority"] = "https://example.com",
            ["Keycloak:GatewayClientId"] = "gateway",
            ["Keycloak:GatewayClientSecret"] = "secret"
        }).Build();

        var service = new TokenExchangeService(httpFactory, fusion, config);

        // Act
        await service.ExchangeTokenAsync("subj-1", "aud-1", "tenant-1", TestContext.Current.CancellationToken); // first call
        await service.ExchangeTokenAsync("subj-1", "aud-1", "tenant-1", TestContext.Current.CancellationToken); // cached
        await service.ExchangeTokenAsync("subj-1", "aud-1", "tenant-2", TestContext.Current.CancellationToken); // new tenant
        await service.ExchangeTokenAsync("subj-1", "aud-2", "tenant-1", TestContext.Current.CancellationToken); // new audience
        await service.ExchangeTokenAsync("subj-2", "aud-1", "tenant-1", TestContext.Current.CancellationToken); // new subject

        // Assert
        handler.SendCount.ShouldBe(4);
    }

    [Fact]
    public async Task ExchangeToken_ThrowsOnErrorPayloadEvenWithOkStatus()
    {
        // Arrange: identitymodel recognizes error payload as failed token response
        using var handler = new DelegatingHandlerStub("{ \"error\": \"invalid_request\" }");
        using var client = new HttpClient(handler);
        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient(Arg.Is<string>(s => s == "KeycloakTokenClient")).Returns(client);

        using var fusion = new FusionCache(new FusionCacheOptions());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()
        {
            ["Keycloak:Authority"] = "https://example.com",
            ["Keycloak:GatewayClientId"] = "gateway",
            ["Keycloak:GatewayClientSecret"] = "secret"
        }).Build();

        var service = new TokenExchangeService(httpFactory, fusion, config);

        // Act / Assert
        await Should.ThrowAsync<HttpRequestException>(async () =>
            await service.ExchangeTokenAsync("subj", "aud", "tenant", TestContext.Current.CancellationToken));
    }
}

// Simple DelegatingHandler stub
internal class DelegatingHandlerStub : DelegatingHandler
{
    private readonly string _responseBody;
    private readonly HttpStatusCode _statusCode;
    public string? LastRequestBody { get; private set; }
    public Uri? LastRequestUri { get; private set; }
    public AuthenticationHeaderValue? LastAuthorizationHeader { get; private set; }
    public int SendCount { get; private set; }

    public DelegatingHandlerStub(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responseBody = responseBody;
        _statusCode = statusCode;
    }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        SendCount++;
        LastRequestUri = request.RequestUri;
        LastAuthorizationHeader = request.Headers.Authorization;
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        var res = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseBody)
        };
        return res;
    }
}
