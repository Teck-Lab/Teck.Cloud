using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using SharedKernel.Api.TestHost;
using Shouldly;
using Xunit;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Checks incoming W3C trace context through the actual API pipeline.</summary>
public sealed partial class TelemetryTests
{
    /// <summary>Incoming requests retain the upstream trace and parent span.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task HttpIngressPreservesRemoteTraceContext()
    {
        using var capture = new ActivityCapture();
        var original = new WebApplicationFactory<ProbeEndpoint>();
        await using var disposeOriginal = original.ConfigureAwait(true);
        var factory = original.WithWebHostBuilder(web =>
            web.ConfigureServices(services => services.AddOpenTelemetry()
                .WithTracing(tracing => tracing.AddProcessor(new SimpleActivityExportProcessor(capture)))));
        await using var disposeFactory = factory.ConfigureAwait(true);
        using var client = factory.CreateClient();
        var trace = ActivityTraceId.CreateRandom();
        var parent = ActivitySpanId.CreateRandom();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/probe", UriKind.Relative));
        request.Headers.Add("traceparent", $"00-{trace}-{parent}-01");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.IsSuccessStatusCode.ShouldBeTrue();
        _ = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while (!capture.Snapshot().Any(activity => activity.Kind == ActivityKind.Server && activity.TraceId == trace))
        {
            await Task.Delay(25, deadline.Token).ConfigureAwait(true);
        }

        factory.Services.GetRequiredService<TracerProvider>().ForceFlush().ShouldBeTrue();
        var server = capture.Snapshot().Where(activity => activity.Kind == ActivityKind.Server && activity.TraceId == trace).ShouldHaveSingleItem();
        server.ParentSpanId.ShouldBe(parent);
        server.Recorded.ShouldBeTrue();
    }
}
