using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using SharedKernel.Infrastructure.Hosting;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Verifies optional trace-to-profile correlation without requiring a native profiler.</summary>
public sealed partial class TelemetryTests
{
    /// <summary>Only explicitly enabled hosts attach profile identifiers to exported root spans.</summary>
    /// <param name="enabled">Whether span-profile correlation is enabled.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProfileCorrelationIsOptIn(bool enabled)
    {
        var activities = new List<Activity>();
        var builder = Host.CreateApplicationBuilder();
        _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Telemetry:Pyroscope:Enabled"] = enabled ? "true" : "false",
        });
        builder.AddFoundationHost(exportTelemetry: false);
        _ = builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddInMemoryExporter(activities));
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using var source = new ActivitySource("Teck.Cloud");
        using (var activity = source.StartActivity("profile-test"))
        {
            _ = activity.ShouldNotBeNull();
        }

        host.Services.GetRequiredService<TracerProvider>().ForceFlush().ShouldBeTrue();
        var exported = activities.Where(activity => string.Equals(activity.OperationName, "profile-test", StringComparison.Ordinal)).ShouldHaveSingleItem();
        exported.GetTagItem("pyroscope.profile.id").ShouldBe(enabled ? exported.SpanId.ToHexString() : null);
        await host.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
