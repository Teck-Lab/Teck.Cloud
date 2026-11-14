using StackExchange.Redis;

namespace SharedKernel.Persistence.Caching;

/// <summary>One lazily and asynchronously connected Redis multiplexer per host.</summary>
/// <param name="configuration">Secret-supplied Redis configuration.</param>
/// <param name="observers">Optional diagnostics observers for the shared connection.</param>
public sealed class RedisConnection(string configuration, IEnumerable<IRedisConnectionObserver>? observers = null) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ConnectionMultiplexer? _connection;
    private bool _disposed;

    /// <summary>Gets a value indicating whether Sentinel manages primary discovery for this connection.</summary>
    public bool UsesSentinel { get; } = !string.IsNullOrWhiteSpace(ConfigurationOptions.Parse(configuration).ServiceName);

    /// <summary>Gets the shared connection; initialization failures can be retried.</summary>
    /// <param name="cancellationToken">The calling operation's cancellation.</param>
    /// <returns>The host-owned multiplexer.</returns>
    public async Task<IConnectionMultiplexer> GetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_connection is null)
            {
                _connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
                foreach (var observer in observers ?? [])
                {
                    observer.Connected(_connection);
                }
            }

            return _connection;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _disposed = true;
            if (_connection is not null)
            {
                await _connection.DisposeAsync().ConfigureAwait(false);
                _connection = null;
            }
        }
        finally
        {
            _ = _gate.Release();
        }
    }
}
