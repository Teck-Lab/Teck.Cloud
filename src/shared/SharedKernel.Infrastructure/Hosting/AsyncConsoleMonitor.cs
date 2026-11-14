using System.Diagnostics.Metrics;
using Serilog.Sinks.Async;

namespace SharedKernel.Infrastructure.Hosting;

internal sealed class AsyncConsoleMonitor : IAsyncLogEventSinkMonitor, IDisposable
{
    private readonly Meter _meter = new("Teck.Cloud");
    private IAsyncLogEventSinkInspector? _inspector;

    public AsyncConsoleMonitor()
    {
        _ = _meter.CreateObservableCounter("teck.logging.console.dropped", () => Volatile.Read(ref _inspector)?.DroppedMessagesCount ?? 0);
        _ = _meter.CreateObservableGauge("teck.logging.console.queued", () => Volatile.Read(ref _inspector)?.Count ?? 0);
    }

    public void StartMonitoring(IAsyncLogEventSinkInspector inspector) => Volatile.Write(ref _inspector, inspector);

    public void StopMonitoring(IAsyncLogEventSinkInspector inspector)
    {
        if (ReferenceEquals(_inspector, inspector))
        {
            Volatile.Write(ref _inspector, null);
        }
    }

    public void Dispose() => _meter.Dispose();
}
