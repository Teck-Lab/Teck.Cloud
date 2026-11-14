using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Persistence.Caching;

namespace SharedKernel.Infrastructure.Hosting;

/// <summary>Configures Redis-backed response caching with explicit public endpoint policies.</summary>
public static class OutputCachingExtensions
{
    /// <summary>The short-lived public policy, selected using the native CacheOutput endpoint extension.</summary>
    public const string PublicPolicy = "foundation-public";

    /// <summary>Adds isolated FusionCache output storage after AddFoundationRedis; no routes are cached automatically.</summary>
    /// <param name="services">Host registrations.</param>
    /// <param name="gatewayName">Stable environment/gateway identity, identical across its replicas.</param>
    /// <returns>The registrations.</returns>
    public static IServiceCollection AddFoundationOutputCaching(this IServiceCollection services, string gatewayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gatewayName);
        var cacheName = "http-output:" + gatewayName;
        _ = services.AddFoundationRedisCache(cacheName);
        _ = services.AddFusionOutputCache(options => options.CacheName = cacheName);
        _ = services.AddSingleton(PublicOutputCachePolicy.Instance);
        _ = services.AddOutputCache(options =>
        {
            options.DefaultExpirationTimeSpan = TimeSpan.FromSeconds(30);
            options.MaximumBodySize = 1024 * 1024;
            options.AddFoundationPublicPolicy(PublicPolicy, TimeSpan.FromSeconds(30), "public");
        });
        return services;
    }

    /// <summary>Adds a named public-only policy with bounded freshness and invalidation tags.</summary>
    /// <param name="options">Native ASP.NET Core output caching options.</param>
    /// <param name="name">Policy selected by CacheOutput on an endpoint.</param>
    /// <param name="duration">Maximum response lifetime.</param>
    /// <param name="tags">Feature-specific invalidation tags within this gateway.</param>
    public static void AddFoundationPublicPolicy(this OutputCacheOptions options, string name, TimeSpan duration, params string[] tags)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        options.AddPolicy(name, policy => policy
            .Expire(duration)
            .SetVaryByQuery("*")
            .SetVaryByHeader("Accept", "Accept-Language")
            .Tag(tags)
            .AddPolicy<PublicOutputCachePolicy>());
    }
}
