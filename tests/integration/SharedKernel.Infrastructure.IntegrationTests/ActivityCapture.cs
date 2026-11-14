using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry;

namespace SharedKernel.Infrastructure.IntegrationTests;

internal sealed class ActivityCapture : BaseExporter<Activity>
{
    private readonly ConcurrentQueue<Activity> _activities = new();

    public Activity[] Snapshot() => [.. _activities];

    public override ExportResult Export(in Batch<Activity> batch)
    {
        foreach (var activity in batch)
        {
            _activities.Enqueue(activity);
        }

        return ExportResult.Success;
    }
}
