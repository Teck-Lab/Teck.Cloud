using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using RabbitMQ.Client;
using Shouldly;
using Wolverine.Util;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Replays the same physical RabbitMQ envelope against Wolverine's durable inbox.</summary>
internal static partial class InboxDuplicateProbe
{
    public static async Task VerifyAsync(IHost host, string broker, string database, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        var connection = await new ConnectionFactory { Uri = new Uri(broker) }.CreateConnectionAsync(deadline.Token).ConfigureAwait(false);
        await using var disposeConnection = connection.ConfigureAwait(false);
        var channel = await connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), deadline.Token).ConfigureAwait(false);
        await using var disposeChannel = channel.ConfigureAwait(false);
        var message = new PublishedExternally { RecordId = Guid.NewGuid() };
        var envelopeId = Guid.NewGuid();
        await PublishAsync(channel, envelopeId, message, deadline.Token).ConfigureAwait(false);
        await WaitHandledAsync(database, envelopeId, deadline.Token).ConfigureAwait(false);
        host.Services.GetRequiredService<DeliveryAttempts>().Count(message.RecordId).ShouldBe(2);
        await PublishAsync(channel, envelopeId, message, deadline.Token).ConfigureAwait(false);

        // Sequential listener: the barrier is handled after the duplicate has entered the same inbox.
        var barrierId = Guid.NewGuid();
        await PublishAsync(channel, barrierId, new PublishedExternally { RecordId = Guid.NewGuid() }, deadline.Token).ConfigureAwait(false);
        await WaitHandledAsync(database, barrierId, deadline.Token).ConfigureAwait(false);
        host.Services.GetRequiredService<DeliveryAttempts>().Count(message.RecordId).ShouldBe(2);
    }

    public static async Task<long> CountPendingOutgoingAsync(string database, CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(database);
        await using var disposeConnection = connection.ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = new NpgsqlCommand("SELECT count(*) FROM messages.wolverine_outgoing_envelopes", connection);
        await using var disposeCommand = command.ConfigureAwait(false);
        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    private static ValueTask PublishAsync(IChannel channel, Guid envelopeId, PublishedExternally message, CancellationToken cancellationToken) =>
        channel.BasicPublishAsync(
            string.Empty,
            "test-deliveries",
            mandatory: true,
            new BasicProperties
            {
                MessageId = envelopeId.ToString(),
                Type = typeof(PublishedExternally).ToMessageTypeName(),
                ContentType = "application/json",
                Persistent = true,
            },
            JsonSerializer.SerializeToUtf8Bytes(message, WireJson.Default.PublishedExternally),
            cancellationToken);

    private static async Task WaitHandledAsync(string database, Guid envelopeId, CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(database);
        await using var disposeConnection = connection.ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = new NpgsqlCommand("SELECT count(*) FROM messages.wolverine_incoming_envelopes WHERE id = @id AND status = 'Handled'", connection);
        await using var disposeCommand = command.ConfigureAwait(false);
        _ = command.Parameters.AddWithValue("id", envelopeId);
        while ((long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))! == 0)
        {
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
    }

    [JsonSerializable(typeof(PublishedExternally))]
    private sealed partial class WireJson : JsonSerializerContext;
}
