using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Locking.Distributed.Redis;
using ZiggyCreatures.Caching.Fusion.Serialization.CysharpMemoryPack;

namespace SharedKernel.Persistence.Caching;

/// <summary>MemoryPack cache storage with shared Redis connections and independent cache namespaces.</summary>
public static class CacheExtensions
{
    /// <summary>Registers a cache with bounded freshness, MemoryPack serialization and no stale fallback.</summary>
    /// <param name="services">Host registrations.</param>
    /// <param name="cacheName">Optional cache name; replicas of a cache must share this name.</param>
    /// <returns>The builder for further explicit customization.</returns>
    public static IFusionCacheBuilder AddFoundationCache(this IServiceCollection services, string? cacheName = null)
    {
        cacheName ??= FusionCacheOptions.DefaultCacheName;
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheName);
        return services.AddFusionCache(cacheName)
            .WithCacheKeyPrefix("teck:mp1:" + Uri.EscapeDataString(cacheName) + ":")
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromMinutes(1),
                IsFailSafeEnabled = false,
                AllowBackgroundDistributedCacheOperations = false,
                AllowBackgroundBackplaneOperations = false,
            }).WithSerializer(new FusionCacheCysharpMemoryPackSerializer());
    }

    /// <summary>Adds Sentinel-managed Redis storage, backplane and locks on one shared managed connection.</summary>
    /// <param name="services">Host registrations.</param>
    /// <param name="configuration">Sentinel endpoints, serviceName and credentials from host secret configuration.</param>
    /// <returns>The configured default cache builder.</returns>
    /// <exception cref="ArgumentException">The Sentinel service name is missing.</exception>
    public static IFusionCacheBuilder AddFoundationRedis(this IServiceCollection services, string configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration);
        var options = ConfigurationOptions.Parse(configuration);
        return string.IsNullOrWhiteSpace(options.ServiceName)
            ? throw new ArgumentException("Service Redis requires Sentinel endpoints and serviceName. Use AddFoundationStandaloneRedis only for isolated standalone tests.", nameof(configuration))
            : RegisterRedis(services, configuration);
    }

    /// <summary>Registers a standalone test dependency; service hosts use AddFoundationRedis with Sentinel.</summary>
    /// <param name="services">Host registrations.</param>
    /// <param name="configuration">Explicit standalone connection used by isolated integration tests.</param>
    /// <returns>The configured default cache builder.</returns>
    public static IFusionCacheBuilder AddFoundationStandaloneRedis(this IServiceCollection services, string configuration) => RegisterRedis(services, configuration);

    /// <summary>Adds an isolated named cache using the host's already registered Redis connection.</summary>
    /// <param name="services">Host registrations after AddFoundationRedis.</param>
    /// <param name="cacheName">Logical service/cache identity shared by replicas, never a pod identifier.</param>
    /// <returns>The configured builder.</returns>
    public static IFusionCacheBuilder AddFoundationRedisCache(this IServiceCollection services, string cacheName) =>
        services.AddFoundationCache(cacheName)
            .WithDistributedCache(provider => provider.GetRequiredService<RedisCache>())
            .WithBackplane(provider => new RedisBackplane(new RedisBackplaneOptions
            {
                ConnectionMultiplexerFactory = () => provider.GetRequiredService<RedisConnection>().GetAsync(),
            }))
            .WithDistributedLocker(provider => new RedisDistributedLocker(new RedisDistributedLockerOptions
            {
                ConnectionMultiplexerFactory = () => provider.GetRequiredService<RedisConnection>().GetAsync(),
            }));

    private static IFusionCacheBuilder RegisterRedis(IServiceCollection services, string configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration);
        _ = services.AddSingleton(provider => new RedisConnection(configuration, provider.GetServices<IRedisConnectionObserver>()));
        _ = services.AddSingleton(provider => new RedisCache(Options.Create(new RedisCacheOptions
        {
            ConnectionMultiplexerFactory = () => provider.GetRequiredService<RedisConnection>().GetAsync(),
        })));
        _ = services.AddScoped<ScopedDistributedLock>();
        return services.AddFoundationRedisCache(FusionCacheOptions.DefaultCacheName);
    }
}
