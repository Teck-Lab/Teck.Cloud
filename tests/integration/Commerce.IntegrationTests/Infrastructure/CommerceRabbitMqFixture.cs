using System.Security.Cryptography;
using Testcontainers.RabbitMq;
using Xunit;

namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>Disposable real RabbitMQ broker for durable commerce messaging.</summary>
public sealed class CommerceRabbitMqFixture : IAsyncLifetime
{
    private readonly string _password = RandomNumberGenerator.GetHexString(32);
    private RabbitMqContainer _container = null!;

    /// <summary>Gets the broker URI handed to every commerce host.</summary>
    public Uri Uri { get; private set; } = null!;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        _container = new RabbitMqBuilder("rabbitmq:4-alpine")
            .WithUsername("commerce")
            .WithPassword(_password)
            .Build();
        await _container.StartAsync(deadline.Token).ConfigureAwait(false);
        Uri = new Uri($"amqp://commerce:{_password}@{_container.Hostname}:{_container.GetMappedPublicPort(5672)}");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Stops the broker to withhold durable delivery.</summary>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The stop task.</returns>
    internal Task StopAsync(CancellationToken cancellationToken) => _container.StopAsync(cancellationToken);

    /// <summary>Restarts the same broker instance for recovery.</summary>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The restart task.</returns>
    internal Task StartAsync(CancellationToken cancellationToken) => _container.StartAsync(cancellationToken);
}
