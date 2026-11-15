using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog.Context;
using SharedKernel.Infrastructure.Hosting;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Verifies real OTLP HTTP exports and enriched asynchronous console output.</summary>
public sealed partial class TelemetryTests
{
    /// <summary>Logs, metrics and traces share identity; logs retain trace context across batching.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task ExportsCorrelatedSignalsAndConsole()
    {
        await using var receiver = new OtlpReceiver();
        await using var console = new StringWriter(CultureInfo.InvariantCulture);
        var previous = Console.Out;
        Console.SetOut(console);
        ActivityTraceId trace;
        try
        {
            trace = await EmitSignalsAsync(receiver.Address).ConfigureAwait(true);
        }
        finally
        {
            Console.SetOut(previous);
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        while (!receiver.Requests.Any(request => string.Equals(request.Path, "/custom/logs", StringComparison.Ordinal)))
        {
            await Task.Delay(50, deadline.Token).ConfigureAwait(true);
        }

        var requests = receiver.Requests.ToArray();
        requests.ShouldContain(request => string.Equals(request.Path, "/v1/traces", StringComparison.Ordinal) && HasTrace(request.Body, trace));
        requests.ShouldContain(request => string.Equals(request.Path, "/v1/metrics", StringComparison.Ordinal) && Encoding.UTF8.GetString(request.Body).Contains("teck.test.requests", StringComparison.Ordinal));
        var metricPayload = string.Concat(requests.Where(request => string.Equals(request.Path, "/v1/metrics", StringComparison.Ordinal)).Select(request => Encoding.UTF8.GetString(request.Body)));
        metricPayload.ShouldContain("dotnet.process.cpu.time", Case.Sensitive);
        metricPayload.ShouldContain("dotnet.process.memory.working_set", Case.Sensitive);
        metricPayload.ShouldNotContain("process.memory.usage", Case.Sensitive);
        var logs = requests.Where(request => string.Equals(request.Path, "/custom/logs", StringComparison.Ordinal)).ToArray();
        logs.ShouldContain(request => HasTrace(request.Body, trace) && Encoding.UTF8.GetString(request.Body).Contains("unique-probe", StringComparison.Ordinal));
        foreach (var request in requests)
        {
            request.Header.ShouldBe("test-value");
        }

        foreach (var request in requests)
        {
            var body = Encoding.UTF8.GetString(request.Body);
            body.ShouldContain("process.pid", Case.Sensitive);
            body.ShouldContain("process.runtime.version", Case.Sensitive);
            body.ShouldContain("os.type", Case.Sensitive);
        }

        requests.ShouldContain(request => string.Equals(request.Path, "/v1/metrics", StringComparison.Ordinal)
            && Encoding.UTF8.GetString(request.Body).Contains("process.thread.count", StringComparison.Ordinal));
        foreach (var request in requests)
        {
            var body = Encoding.UTF8.GetString(request.Body);
            body.ShouldContain("teck-telemetry-test", Case.Sensitive);
            body.ShouldContain("service.version", Case.Sensitive);
            body.ShouldContain("42.7.3", Case.Sensitive);
        }

        console.ToString().ShouldContain(trace.ToHexString(), Case.Sensitive);
        console.ToString().ShouldContain("unique-probe", Case.Sensitive);
        console.ToString().ShouldContain("Operation", Case.Sensitive);
        VerifyDiagnosticOutput(console.ToString());
        VerifyDiagnosticOutput(string.Concat(logs.Select(request => Encoding.UTF8.GetString(request.Body))));
    }

    private static async Task<ActivityTraceId> EmitSignalsAsync(Uri receiver)
    {
        var builder = Host.CreateApplicationBuilder();
        _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["OTEL_SERVICE_NAME"] = "teck-telemetry-test",
            ["Telemetry:ServiceVersion"] = "42.7.3",
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = receiver.AbsoluteUri,
            ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
            ["OTEL_EXPORTER_OTLP_HEADERS"] = "x-test-otel=test-value",
            ["OTEL_EXPORTER_OTLP_LOGS_ENDPOINT"] = new Uri(receiver, "custom/logs").AbsoluteUri,
        });
        builder.AddFoundationHost();
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        using var meter = new Meter("Teck.Cloud");
        var counter = meter.CreateCounter<long>("teck.test.requests");
        using var source = new ActivitySource("Teck.Cloud");
        ActivityTraceId trace;
        using (var activity = source.StartActivity("export-test"))
        {
            _ = activity.ShouldNotBeNull();
            trace = activity.TraceId;
            using var property = LogContext.PushProperty("Operation", "verification");
            EmitDiagnosticProbe(host.Services.GetRequiredService<ILogger<TelemetryTests>>());
            counter.Add(1);
        }

        host.Services.GetRequiredService<TracerProvider>().ForceFlush().ShouldBeTrue();
        host.Services.GetRequiredService<MeterProvider>().ForceFlush().ShouldBeTrue();
        await host.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return trace;
    }

    private static void EmitDiagnosticProbe(ILogger logger)
    {
        var properties = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["Password"] = "never-export-password-734",
            ["Nested"] = new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = "never-export-auth-734" },
        };
        using var payload = LogContext.PushProperty("Payload", properties, destructureObjects: true);
        using var database = new DiagnosticDbContext();
        var entry = database.Entry(new DiagnosticRecord { Id = 1, Name = "never-export-entity-value" });
        var error = new DbUpdateException("Cannot save for secret-probe@example.test", new InvalidOperationException("Contact inner-probe@example.test"), [entry]);
        error.Data["DebugPayload"] = "never-export-exception-data";
        LogProbe(logger, "unique-probe", error);
        var rpc = new RpcException(
            new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "RPC failed for grpc-probe@example.test"),
            new Metadata { { "authorization", "never-export-rpc-trailer" } });
        LogProbe(logger, "rpc-probe", rpc);
    }

    private static void VerifyDiagnosticOutput(string output)
    {
        output.ShouldContain("ProcessId", Case.Sensitive);
        output.ShouldContain("ProcessName", Case.Sensitive);
        output.ShouldContain("ThreadId", Case.Sensitive);
        output.ShouldContain("ExceptionDetail", Case.Sensitive);
        output.ShouldContain("DbUpdateException", Case.Sensitive);
        output.ShouldContain("***MASKED***", Case.Sensitive);
        output.ShouldContain("StatusCode", Case.Sensitive);
        output.ShouldContain("Unavailable", Case.Sensitive);
        output.ShouldNotContain("never-export-exception-data", Case.Sensitive);
        output.ShouldNotContain("never-export-rpc-trailer", Case.Sensitive);
        output.ShouldNotContain("grpc-probe@example.test", Case.Sensitive);
        output.ShouldNotContain("never-export-entity-value", Case.Sensitive);
        output.ShouldNotContain("EntryProperties", Case.Sensitive);
        output.ShouldNotContain("never-export-password-734", Case.Sensitive);
        output.ShouldNotContain("never-export-auth-734", Case.Sensitive);
        output.ShouldNotContain("secret-probe@example.test", Case.Sensitive);
        output.ShouldNotContain("inner-probe@example.test", Case.Sensitive);
    }

    private static bool HasTrace(byte[] body, ActivityTraceId trace) => body.AsSpan().IndexOf(Convert.FromHexString(trace.ToHexString())) >= 0;
}
