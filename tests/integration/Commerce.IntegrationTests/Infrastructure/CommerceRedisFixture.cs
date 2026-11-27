using Testcontainers.Redis;
using Xunit;

namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>Isolated standalone Redis for both gateways' validated tenant directory.</summary>
public sealed class CommerceRedisFixture : IAsyncLifetime
{
    private RedisContainer _container = null!;

    /// <summary>Gets the StackExchange.Redis standalone connection string.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        _container = new RedisBuilder("redis:8-alpine").Build();
        await _container.StartAsync(deadline.Token).ConfigureAwait(false);
        ConnectionString = _container.Hostname + ":" + _container.GetMappedPublicPort(6379);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
        }
    }
}
