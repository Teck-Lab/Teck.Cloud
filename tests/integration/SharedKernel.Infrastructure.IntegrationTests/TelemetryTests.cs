using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using SharedKernel.Infrastructure.Hosting;
using SharedKernel.Persistence.Caching;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Verifies the shared instrumentation actually exports correlated activities.</summary>
[Collection(TelemetryGroup.Name)]
public sealed partial class TelemetryTests
{
    private static readonly Action<ILogger, string, Exception?> LogProbe = LoggerMessage.Define<string>(LogLevel.Information, new EventId(101, "Probe"), "Telemetry probe {Marker}");

    /// <summary>Wolverine and FusionCache activities retain the application request trace.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task FrameworkActivitiesAreCollected()
    {
        var activities = new List<Activity>();
        var builder = Host.CreateApplicationBuilder();
        builder.AddFoundationHost(exportTelemetry: false);
        _ = builder.Services.AddFoundationCache();
        _ = builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddInMemoryExporter(activities));
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using var source = new ActivitySource("Wolverine");
        using (var request = source.StartActivity("telemetry-test"))
        {
            _ = request.ShouldNotBeNull();
            await host.Services.GetRequiredService<IFusionCache>().SetAsync("telemetry-test", 42, token: TestContext.Current.CancellationToken).ConfigureAwait(true);
        }

        _ = host.Services.GetRequiredService<TracerProvider>().ForceFlush();
        activities.ShouldContain(activity => activity.Source.Name.StartsWith(FusionCacheDiagnostics.ActivitySourceName, StringComparison.Ordinal));
        var parent = activities.Where(activity => string.Equals(activity.OperationName, "telemetry-test", StringComparison.Ordinal)).ShouldHaveSingleItem();
        foreach (var activity in activities.Where(activity => activity.Source.Name.StartsWith(FusionCacheDiagnostics.ActivitySourceName, StringComparison.Ordinal)))
        {
            activity.TraceId.ShouldBe(parent.TraceId);
        }

        await host.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
