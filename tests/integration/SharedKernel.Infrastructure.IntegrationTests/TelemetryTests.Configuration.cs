using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using SharedKernel.Infrastructure.Hosting;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Verifies configuration and upstream sampling boundaries.</summary>
public sealed partial class TelemetryTests
{
    /// <summary>Invalid collector configuration fails during registration.</summary>
    /// <param name="endpoint">The configured collector.</param>
    /// <param name="protocol">The configured wire protocol.</param>
    [Theory]
    [InlineData("", "grpc")]
    [InlineData("relative-path", "grpc")]
    [InlineData("http://localhost:4318", "http/json")]
    [InlineData("http://user:password@localhost:4318", "grpc")]
    public void RequiredExportRejectsInvalidConfiguration(string endpoint, string protocol)
    {
        var builder = Host.CreateApplicationBuilder();
        _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint,
            ["OTEL_EXPORTER_OTLP_PROTOCOL"] = protocol,
        });
        _ = Should.Throw<InvalidOperationException>(() => builder.AddFoundationHost(exportTelemetry: true)).ShouldBeOfType<InvalidOperationException>();
    }

    /// <summary>Additional configured sinks cannot bypass the final masking pass.</summary>
    /// <param name="section">The unsupported sink configuration section.</param>
    [Theory]
    [InlineData("WriteTo")]
    [InlineData("AuditTo")]
    public void AdditionalConfiguredSinksAreRejected(string section)
    {
        var builder = Host.CreateApplicationBuilder();
        _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [$"Serilog:{section}:0:Name"] = "Console",
        });
        _ = Should.Throw<InvalidOperationException>(() => builder.AddFoundationHost(exportTelemetry: false)).ShouldBeOfType<InvalidOperationException>();
    }

    /// <summary>Tests can disable export while retaining instrumentation and upstream sampling decisions.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task DisabledExportStillHonorsRemoteSampling()
    {
        var builder = Host.CreateApplicationBuilder();
        _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "invalid-endpoint",
            ["OTEL_TRACES_SAMPLER"] = "parentbased_always_on",
        });
        builder.AddFoundationHost(exportTelemetry: false);
        _ = Should.Throw<InvalidOperationException>(() => builder.AddFoundationTelemetry()).ShouldBeOfType<InvalidOperationException>();
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        _ = host.Services.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource("Teck.Cloud");
        var parent = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None, isRemote: true);
        using var activity = source.StartActivity("unsampled-remote", ActivityKind.Internal, parent);
        _ = activity.ShouldNotBeNull();
        activity.TraceId.ShouldBe(parent.TraceId);
        activity.Recorded.ShouldBeFalse();
        await host.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
