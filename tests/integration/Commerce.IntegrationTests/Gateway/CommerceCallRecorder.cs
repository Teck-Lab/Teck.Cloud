using System.Collections.Concurrent;

namespace Commerce.IntegrationTests.Gateway;

/// <summary>Records every gateway-to-backend call so scope, generation and single-flight behavior are observable.</summary>
internal sealed class CommerceCallRecorder
{
    private readonly ConcurrentQueue<RecordedCommerceCall> _calls = new();

    internal IReadOnlyList<RecordedCommerceCall> Calls => [.. _calls];

    internal void Record(RecordedCommerceCall call) => _calls.Enqueue(call);

    internal IReadOnlyList<RecordedCommerceCall> For(string method) =>
        [.. Calls.Where(call => string.Equals(call.Method, method, StringComparison.Ordinal))];

    internal int Count(string method, Guid orderId) =>
        Calls.Count(call => string.Equals(call.Method, method, StringComparison.Ordinal) && string.Equals(call.OrderId, orderId.ToString("D"), StringComparison.Ordinal));
}
