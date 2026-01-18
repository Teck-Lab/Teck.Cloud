using System.Text.Json;
using System.Text.Json.Serialization;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel.Core.Exceptions;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace SharedKernel.Infrastructure.Caching
{
    /// <summary>
    /// The extensions.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Add caching service.
        /// </summary>
        /// <param name="builder">The builder.</param>
        public static void AddCachingInfrastructure(this WebApplicationBuilder builder)
        {
            var connectionString = builder.Configuration.GetConnectionString("redis")
                ?? throw new ConfigurationMissingException("Redis");

            builder.AddRedisDistributedCache("redis");

            builder.Services
                .AddFusionCache()
                .WithRegisteredDistributedCache()
                .WithBackplane(new RedisBackplane(new RedisBackplaneOptions { Configuration = connectionString }))
                .WithSerializer(new FusionCacheSystemTextJsonSerializer(new JsonSerializerOptions
                {
                    ReferenceHandler = ReferenceHandler.IgnoreCycles
                }))
                .WithDefaultEntryOptions(new FusionCacheEntryOptions()
                    .SetDuration(TimeSpan.FromMinutes(2))
                    .SetPriority(CacheItemPriority.High)
                    .SetFailSafe(true, TimeSpan.FromHours(2))
                    .SetFactoryTimeouts(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(2)));

            var redisConnection = ConnectionMultiplexer.Connect(connectionString);

            builder.Services.AddSingleton<IDistributedLockProvider>(_ =>
                new RedisDistributedSynchronizationProvider(redisConnection.GetDatabase()));

            builder.Services.AddFusionCacheSystemTextJsonSerializer();

            builder.Services.AddHealthChecks().AddRedis(connectionString);
        }
    }
}
