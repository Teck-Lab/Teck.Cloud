using Microsoft.Extensions.Hosting;
using SharedKernel.Infrastructure.Observability.OpenTelemetry;
using SharedKernel.Infrastructure.Observability.Serilog;

namespace SharedKernel.Infrastructure.Observability;

/// <summary>
/// Centralized observability setup for tracing, metrics and Serilog.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Adds standardized OpenTelemetry and Serilog observability wiring.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same host builder.</returns>
    public static IHostApplicationBuilder AddTeckCloudObservability(this IHostApplicationBuilder builder)
    {
        builder.ConfigureTeckCloudOpenTelemetry();
        builder.ConfigureTeckCloudSerilog();

        return builder;
    }
}
