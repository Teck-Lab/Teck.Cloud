using System.Security.Cryptography;
using RabbitMQ.Client;
using Teck.Cloud.Commerce.Contracts;
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

        // The first host may listen before its peer has declared the commerce topology.
        await DeclareCommerceTopologyAsync(deadline.Token).ConfigureAwait(false);
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

    private async Task DeclareCommerceTopologyAsync(CancellationToken cancellationToken)
    {
        const string deadLetterExchange = "wolverine-dead-letter-queue";
        var factory = new ConnectionFactory { Uri = Uri };
        var connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var connectionLease = connection.ConfigureAwait(false);
        var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var channelLease = channel.ConfigureAwait(false);
        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["x-dead-letter-exchange"] = deadLetterExchange,
        };
        foreach (var name in new[] { deadLetterExchange, CommerceMessaging.LicenseDesired, CommerceMessaging.LicenseState })
        {
            await channel.ExchangeDeclareAsync(name, ExchangeType.Fanout, durable: true, cancellationToken: cancellationToken).ConfigureAwait(false);
            _ = await channel.QueueDeclareAsync(
                name,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: string.Equals(name, deadLetterExchange, StringComparison.Ordinal) ? null : arguments,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            await channel.QueueBindAsync(name, name, name, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
