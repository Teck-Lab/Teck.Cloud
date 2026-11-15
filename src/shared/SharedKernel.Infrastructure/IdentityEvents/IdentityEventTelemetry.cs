using System.Diagnostics.Metrics;

namespace SharedKernel.Infrastructure.IdentityEvents;

internal static class IdentityEventTelemetry
{
    private static readonly Meter Meter = new("Teck.Cloud");
    private static readonly Counter<long> Pages = Meter.CreateCounter<long>("teck.identity.reconciliation.pages", description: "Completed reconciliation attempts by bounded outcome.");
    private static readonly Counter<long> Organizations = Meter.CreateCounter<long>("teck.identity.reconciliation.organizations", description: "Canonical organizations refreshed, including progress before failures.");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("teck.identity.reconciliation.duration", "s", "Elapsed canonical reconciliation time.");

    internal static void Record(int refreshed, string outcome, double seconds)
    {
        var tag = new KeyValuePair<string, object?>("outcome", outcome);
        Pages.Add(1, tag);
        Organizations.Add(refreshed, tag);
        Duration.Record(seconds, tag);
    }
}
