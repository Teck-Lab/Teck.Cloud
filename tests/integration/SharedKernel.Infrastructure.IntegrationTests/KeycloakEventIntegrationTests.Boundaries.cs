using System.Security.Cryptography;
using System.Text;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Shouldly;
using Testcontainers.RabbitMq;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Real inbox poison handling and broker publisher access controls.</summary>
public sealed partial class KeycloakEventIntegrationTests
{
    private static async Task ControlBrokerAsync(RabbitMqContainer rabbit, string operation, CancellationToken cancellationToken)
    {
        var result = await rabbit.ExecAsync(["rabbitmqctl", operation], cancellationToken).ConfigureAwait(false);
        result.ExitCode.ShouldBe(0);
    }

    private static async Task VerifyPoisonAsync(string broker, string database, CancellationToken cancellationToken)
    {
        const string secretMarker = "must-never-persist-this-source-value";
        await PublishAsync(broker, Encoding.UTF8.GetBytes("{\"representation\":\"" + secretMarker + "\"}"), cancellationToken).ConfigureAwait(false);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        var connection = new NpgsqlConnection(database);
        await using var disposeConnection = connection.ConfigureAwait(false);
        await connection.OpenAsync(deadline.Token).ConfigureAwait(false);
        var count = new NpgsqlCommand("SELECT count(*) FROM identity_events.wolverine_dead_letters", connection);
        await using var disposeCount = count.ConfigureAwait(false);
        while ((long)(await count.ExecuteScalarAsync(deadline.Token).ConfigureAwait(false))! == 0)
        {
            await Task.Delay(40, deadline.Token).ConfigureAwait(false);
        }

        var contents = new NpgsqlCommand("SELECT encode(body,'escape') || coalesce(exception_message,'') FROM identity_events.wolverine_dead_letters", connection);
        await using var disposeContents = contents.ConfigureAwait(false);
        var stored = (string)(await contents.ExecuteScalarAsync(deadline.Token).ConfigureAwait(false))!;
        stored.ShouldNotContain(secretMarker, Case.Sensitive);
        stored.ShouldNotContain("representation", Case.Sensitive);
        stored.ShouldContain("Rejected identity event metadata", Case.Sensitive);
    }

    private static async Task VerifyPublisherBoundaryAsync(RabbitMqContainer rabbit, CancellationToken cancellationToken)
    {
        var password = RandomNumberGenerator.GetHexString(48);
        var created = await rabbit.ExecAsync(["rabbitmqctl", "add_user", "ordinary-publisher", password], cancellationToken).ConfigureAwait(false);
        created.ExitCode.ShouldBe(0);
        var permissions = await rabbit.ExecAsync(["rabbitmqctl", "set_permissions", "-p", "/", "ordinary-publisher", "^$", "^ordinary-events$", "^$"], cancellationToken).ConfigureAwait(false);
        permissions.ExitCode.ShouldBe(0);
        var builder = new UriBuilder(rabbit.GetConnectionString()) { UserName = "ordinary-publisher", Password = password };
        var connection = await new ConnectionFactory { Uri = builder.Uri }.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var disposeConnection = connection.ConfigureAwait(false);
        var channel = await connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), cancellationToken).ConfigureAwait(false);
        await using var disposeChannel = channel.ConfigureAwait(false);

        // RabbitMQ rejects publishing to the identity exchange before any consumer/handler sees a body.
        _ = await Should.ThrowAsync<AlreadyClosedException>(() => channel.BasicPublishAsync("identity-events", "identity-events", mandatory: true, new BasicProperties(), "{}"u8.ToArray(), cancellationToken).AsTask()).ConfigureAwait(false);
    }
}
