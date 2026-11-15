using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Shouldly;

namespace SharedKernel.Infrastructure.IntegrationTests;

internal sealed class ReconciliationMeasurements : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly ConcurrentDictionary<string, long> _outcomes = new(StringComparer.Ordinal);

    internal ReconciliationMeasurements()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (string.Equals(instrument.Meter.Name, "Teck.Cloud", StringComparison.Ordinal)
                && string.Equals(instrument.Name, "teck.identity.reconciliation.pages", StringComparison.Ordinal))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            tags.Length.ShouldBe(1);
            tags[0].Key.ShouldBe("outcome");
            var outcome = tags[0].Value.ShouldBeOfType<string>();
            _ = _outcomes.AddOrUpdate(outcome, static (_, amount) => amount, static (_, previous, amount) => previous + amount, value);
        });
        _listener.Start();
    }

    public void Dispose() => _listener.Dispose();

    internal void VerifyBoundedOutcomes()
    {
        _outcomes["success"].ShouldBeGreaterThanOrEqualTo(1);
        _outcomes["failure"].ShouldBeGreaterThanOrEqualTo(1);
        _outcomes["cancelled"].ShouldBeGreaterThanOrEqualTo(1);
        _outcomes.Keys.Count.ShouldBe(3);
    }
}
