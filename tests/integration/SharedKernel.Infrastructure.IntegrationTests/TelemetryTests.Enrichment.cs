using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedKernel.Infrastructure.Hosting;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Exercises optional HTTP enrichment and exception traversal boundaries.</summary>
public sealed partial class TelemetryTests
{
    /// <summary>Client enrichment is explicit and ignores untrusted forwarding headers.</summary>
    /// <param name="clientIp">Whether to record the resolved connection IP.</param>
    /// <param name="userAgent">Whether to record User-Agent.</param>
    /// <returns>The verification task.</returns>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ClientInformationIsOptIn(bool clientIp, bool userAgent)
    {
        var output = await CaptureDiagnosticLogsAsync(EmitClientProbe, clientIp, userAgent).ConfigureAwait(true);
        output.Contains("192.0.2.10", StringComparison.Ordinal).ShouldBe(clientIp);
        output.Contains("test-agent/1.0", StringComparison.Ordinal).ShouldBe(userAgent);
        output.ShouldNotContain("198.51.100.20", Case.Sensitive);
        output.ShouldNotContain("client-secret@example.test", Case.Sensitive);
        output.ShouldNotContain("never-log-header-auth", Case.Sensitive);
        output.ShouldNotContain("never-log-header-cookie", Case.Sensitive);
        output.ShouldNotContain("CorrelationId", Case.Sensitive);
    }

    /// <summary>Unknown exception properties are not visited by reflection.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task ExceptionEnrichmentDoesNotReadUnknownProperties()
    {
        var exception = new UnknownDiagnosticException();
        var output = await CaptureDiagnosticLogsAsync(services => LogProbe(services.GetRequiredService<ILogger<TelemetryTests>>(), "unknown-exception", exception)).ConfigureAwait(true);
        exception.DetailRead.ShouldBeFalse();
        output.ShouldNotContain("never-visit-custom-detail", Case.Sensitive);
        output.ShouldContain(nameof(UnknownDiagnosticException), Case.Sensitive);
    }

    private static void EmitClientProbe(IServiceProvider services)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.20";
        context.Request.Headers.UserAgent = "test-agent/1.0 client-secret@example.test";
        context.Request.Headers.Authorization = "never-log-header-auth";
        context.Request.Headers.Cookie = "never-log-header-cookie";
        var accessor = services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = context;
        try
        {
            LogProbe(services.GetRequiredService<ILogger<TelemetryTests>>(), "client-probe", null);
        }
        finally
        {
            accessor.HttpContext = null;
        }
    }

    private static async Task<string> CaptureDiagnosticLogsAsync(Action<IServiceProvider> emit, bool clientIp = false, bool userAgent = false)
    {
        var console = new StringWriter(CultureInfo.InvariantCulture);
        await using var disposeConsole = console.ConfigureAwait(false);
        var original = Console.Out;
        Console.SetOut(console);
        try
        {
            var builder = Host.CreateApplicationBuilder();
            _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Telemetry:ClientInfo:Enabled"] = clientIp.ToString(),
                ["Telemetry:ClientInfo:UserAgent"] = userAgent.ToString(),
            });
            builder.AddFoundationHost(exportTelemetry: false);
            _ = builder.Services.AddHttpContextAccessor();
            using var host = builder.Build();
            await host.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
            emit(host.Services);
            await host.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Console.SetOut(original);
        }

        return console.ToString();
    }
}
