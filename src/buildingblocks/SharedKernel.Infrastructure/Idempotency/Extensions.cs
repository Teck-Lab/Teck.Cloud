using IdempotentAPI.Cache.FusionCache.Extensions.DependencyInjection;
using IdempotentAPI.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Infrastructure.Idempotency
{
    /// <summary>
    /// The extensions.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Add idempotency support.
        /// </summary>
        /// <param name="services">The services.</param>
        public static IServiceCollection AddIdempotencySupport(this IServiceCollection services)
        {
            services.AddIdempotentAPIUsingRegisteredFusionCache();

            services.AddIdempotentMinimalAPI(new IdempotentAPI.Core.IdempotencyOptions
            {
                HeaderKeyName = "Idempotency-Key"
            });

            return services;
        }
    }
}
