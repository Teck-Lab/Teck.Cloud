using StackExchange.Redis;

namespace SharedKernel.Persistence.Caching;

/// <summary>Observes the shared lazy Redis connection without creating another connection.</summary>
public interface IRedisConnectionObserver
{
    /// <summary>Attaches diagnostics after the host-owned connection is created.</summary>
    /// <param name="connection">The shared multiplexer; the observer does not own it.</param>
    void Connected(IConnectionMultiplexer connection);
}
