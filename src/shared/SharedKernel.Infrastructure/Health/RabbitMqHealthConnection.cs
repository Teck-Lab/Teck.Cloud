using RabbitMQ.Client;

namespace SharedKernel.Infrastructure.Health;

/// <summary>Owns one retryable asynchronous broker probe connection for the host lifetime.</summary>
/// <param name="broker">The host-owned dependency.</param>
internal sealed class RabbitMqHealthConnection(Uri broker) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private bool _disposed;

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
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

    internal async Task<IConnection> GetAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_connection is null)
            {
                var factory = new ConnectionFactory { Uri = broker, AutomaticRecoveryEnabled = true };
                _connection = await factory.CreateConnectionAsync("foundation-health", cancellationToken).ConfigureAwait(false);
            }

            return _connection;
        }
        finally
        {
            _ = _gate.Release();
        }
    }
}
