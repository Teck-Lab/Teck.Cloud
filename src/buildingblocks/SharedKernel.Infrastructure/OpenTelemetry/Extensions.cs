using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace SharedKernel.Infrastructure.OpenTelemetry
{
    internal static class Extensions
    {
        internal static void AddOpenTelemetryExtension(this WebApplicationBuilder builder, string name)
        {
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(name))
                .WithTracing(tracing =>
                {
                    tracing.AddFusionCacheInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddRedisInstrumentation()
                    .AddAspNetCoreInstrumentation();
                })
                .WithMetrics(metrics =>
                {
                    metrics.AddFusionCacheInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddAspNetCoreInstrumentation();
                });
            // OpenTelemetry logging is configured through the service collection above
        }
    }
}
