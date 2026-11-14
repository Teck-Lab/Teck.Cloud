using System.Diagnostics;
using ErrorOr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using Shouldly;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;

namespace SharedKernel.Infrastructure.IntegrationTests;

internal static class TelemetryEvidence
{
    public static async Task<ErrorOr<Guid>> InvokeAsync(IMessageBus bus, Guid id, CancellationToken cancellation)
    {
        using var source = new ActivitySource("Teck.Cloud");
        using var request = source.StartActivity("messaging-trace-probe");
        _ = request.ShouldNotBeNull();
        return await bus.InvokeAsync<ErrorOr<Guid>>(new CreateRecord(id, Fail: false), cancellation).ConfigureAwait(false);
    }

    public static async Task VerifyAsync(IHost host, CancellationToken cancellation)
    {
        var capture = host.Services.GetRequiredService<ActivityCapture>();
        var root = capture.Snapshot().Where(activity => string.Equals(activity.OperationName, "messaging-trace-probe", StringComparison.Ordinal)).ShouldHaveSingleItem();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        while (!capture.Snapshot().Any(activity => activity.TraceId == root.TraceId && activity.Source.Name.Contains("StackExchangeRedis", StringComparison.Ordinal)))
        {
            await Task.Delay(100, deadline.Token).ConfigureAwait(false);
        }

        host.Services.GetRequiredService<TracerProvider>().ForceFlush().ShouldBeTrue();
        var trace = capture.Snapshot().Where(activity => activity.TraceId == root.TraceId).ToArray();
        trace.ShouldContain(activity => string.Equals(activity.Source.Name, "Npgsql", StringComparison.Ordinal));
        trace.ShouldContain(activity => string.Equals(activity.Source.Name, "Wolverine", StringComparison.Ordinal) && activity.DisplayName.Contains(nameof(PublishedExternally), StringComparison.Ordinal));
        trace.ShouldContain(activity => activity.Source.Name.StartsWith(FusionCacheDiagnostics.ActivitySourceName, StringComparison.Ordinal));
        trace.ShouldNotContain(activity => activity.Source.Name.Contains("EntityFrameworkCore", StringComparison.Ordinal));
    }
}
