using System.Collections.Concurrent;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Counts controlled transient failures in the broker consumer fixture.</summary>
public sealed class DeliveryAttempts
{
    private readonly ConcurrentDictionary<Guid, int> _counts = new();

    /// <summary>Records an attempt.</summary>
    /// <param name="id">The business identifier.</param>
    /// <returns>The attempt count.</returns>
    public int Increment(Guid id) => _counts.AddOrUpdate(id, 1, (_, count) => count + 1);

    /// <summary>Returns a recorded count.</summary>
    /// <param name="id">The business identifier.</param>
    /// <returns>The attempts so far.</returns>
    public int Count(Guid id) => _counts.GetValueOrDefault(id);
}
