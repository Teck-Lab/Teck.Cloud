using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Api.TestHost;
using SharedKernel.Infrastructure.Hosting;
using SharedKernel.Persistence.Caching;
using Shouldly;
using Testcontainers.Redis;
using Xunit;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Verifies FastEndpoints output caching across independent hosts sharing Redis.</summary>
[Collection(TelemetryGroup.Name)]
public sealed class OutputCacheTests
{
    /// <summary>Replicas share responses but gateway identities and query strings are isolated.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task ReplicasShareResponsesAndRespectIsolation()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var redis = new RedisBuilder("redis:8-alpine").Build();
        await redis.StartAsync(cancellation).ConfigureAwait(true);
        await using var first = new CacheHost(redis.GetConnectionString(), "customer");
        await using var second = new CacheHost(redis.GetConnectionString(), "customer");
        await using var admin = new CacheHost(redis.GetConnectionString(), "admin");
        using var left = first.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var right = second.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var other = admin.CreateClient();
        var uri = new Uri("/cache-probe?version=1", UriKind.Relative);
        var original = await left.GetStringAsync(uri, cancellation).ConfigureAwait(true);
        (await right.GetStringAsync(uri, cancellation).ConfigureAwait(true)).ShouldBe(original);
        (await other.GetStringAsync(uri, cancellation).ConfigureAwait(true)).ShouldNotBe(original, StringComparer.Ordinal);
        (await right.GetStringAsync(new Uri("/cache-probe?version=2", UriKind.Relative), cancellation).ConfigureAwait(true)).ShouldNotBe(original, StringComparer.Ordinal);
        foreach (var (header, value) in new[] { ("Authorization", "Bearer fixture"), ("Cookie", "session=fixture") })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add(header, value);
            using var response = await right.SendAsync(request, cancellation).ConfigureAwait(true);
            (await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(true)).ShouldNotBe(original, StringComparer.Ordinal);
        }

        foreach (var query in new[] { "private=true", "cookie=true" })
        {
            var path = new Uri("/cache-probe?" + query, UriKind.Relative);
            var uncached = await left.GetStringAsync(path, cancellation).ConfigureAwait(true);
            (await left.GetStringAsync(path, cancellation).ConfigureAwait(true)).ShouldNotBe(uncached, StringComparer.Ordinal);
        }

        var authenticated = await first.Server.SendAsync(
            context =>
        {
            context.Request.Method = "GET";
            context.Request.Scheme = "http";
            context.Request.Host = new HostString("localhost");
            context.Request.Path = "/cache-probe";
            context.Request.QueryString = new QueryString("?version=1");
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "alice")], "fixture"));
        },
            cancellation).ConfigureAwait(true);
        using var reader = new StreamReader(authenticated.Response.Body);
        (await reader.ReadToEndAsync(cancellation).ConfigureAwait(true)).ShouldNotBe(original, StringComparer.Ordinal);
        (await right.GetStringAsync(uri, cancellation).ConfigureAwait(true)).ShouldBe(original);
        await first.Services.GetRequiredService<IOutputCacheStore>().EvictByTagAsync("public", cancellation).ConfigureAwait(true);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        while (string.Equals(original, await right.GetStringAsync(uri, deadline.Token).ConfigureAwait(true), StringComparison.Ordinal))
        {
            await Task.Delay(25, deadline.Token).ConfigureAwait(true);
        }
    }

    private sealed class CacheHost(string redis, string gateway) : WebApplicationFactory<ProbeEndpoint>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            _ = services.AddFoundationStandaloneRedis(redis);
            _ = services.AddFoundationOutputCaching(gateway);
        });
    }
}
