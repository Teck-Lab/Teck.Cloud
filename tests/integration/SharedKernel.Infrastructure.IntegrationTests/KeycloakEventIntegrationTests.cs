using System.Text.Json.Nodes;
using JasperFx.CodeGeneration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RabbitMQ.Client;
using SharedKernel.Api.TestHost;
using SharedKernel.Infrastructure.IdentityEvents;
using SharedKernel.Persistence.Caching;
using Shouldly;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Wolverine;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Real broker/inbox and canonical Keycloak refresh, independent of experimental event publishing.</summary>
/// <param name="fixture">The owned real identity provider.</param>
public sealed partial class KeycloakEventIntegrationTests(KeycloakFixture fixture) : IClassFixture<KeycloakFixture>
{
#if VERIFY_STATIC_HANDLERS
    private const TypeLoadMode HandlerMode = TypeLoadMode.Static;
#else
    private const TypeLoadMode HandlerMode = TypeLoadMode.Auto;
#endif

    /// <summary>Duplicates and reordered changes cannot reinstate a tenant; reconciliation survives broker loss.</summary>
    /// <returns>The verification task.</returns>
    [Fact]
    public async Task BrokerDeliveryDedupePoisonAndReconciliationRemainSafe()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var database = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await using var rabbit = new RabbitMqBuilder("rabbitmq:4-alpine").Build();
        await Task.WhenAll(database.StartAsync(cancellation), rabbit.StartAsync(cancellation)).ConfigureAwait(true);
        var services = new ServiceCollection();
        _ = services.AddFoundationCache();
        await using var provider = services.BuildServiceProvider();
        using var directory = new KeycloakStoreClient(fixture.ServerAddress, await fixture.AdminAuthorizationAsync(cancellation).ConfigureAwait(true), provider.GetRequiredService<IFusionCache>(), TimeProvider.System);
        using var host = IdentityEventTestHost.Build(database.GetConnectionString(), rabbit.GetConnectionString(), registrations => registrations.AddSingleton(directory.Store), HandlerMode, typeof(KeycloakEventIntegrationTests).Assembly);
        await host.StartAsync(cancellation).ConfigureAwait(true);
        try
        {
            (await directory.Store.IsActiveAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true)).ShouldBeTrue();
            using var disabled = await fixture.UpdateOrganizationAsync(new JsonObject { ["enabled"] = false }, cancellation).ConfigureAwait(true);
            var changed = Notice("UPDATE", time: 100);
            await PublishAsync(rabbit.GetConnectionString(), Serialize(changed), cancellation).ConfigureAwait(true);
            await WaitHandledAsync(database.GetConnectionString(), changed, cancellation).ConfigureAwait(true);
            (await directory.Store.IsActiveAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true)).ShouldBeFalse();
            var previousCount = directory.Handler.Count;
            await PublishAsync(rabbit.GetConnectionString(), Serialize(changed), cancellation).ConfigureAwait(true);
            var reordered = Notice("CREATE", time: 1);
            await PublishAsync(rabbit.GetConnectionString(), Serialize(reordered), cancellation).ConfigureAwait(true);
            await WaitHandledAsync(database.GetConnectionString(), reordered, cancellation).ConfigureAwait(true);
            directory.Handler.Count.ShouldBe(previousCount + 1);
            await VerifyPoisonAsync(rabbit.GetConnectionString(), database.GetConnectionString(), cancellation).ConfigureAwait(true);
            await VerifyPublisherBoundaryAsync(rabbit, cancellation).ConfigureAwait(true);

            using var enabled = await fixture.UpdateOrganizationAsync(new JsonObject { ["enabled"] = true }, cancellation).ConfigureAwait(true);
            await directory.Store.InvalidateAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true);
            (await directory.Store.IsActiveAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true)).ShouldBeTrue();
            await ControlBrokerAsync(rabbit, "stop_app", cancellation).ConfigureAwait(true);
            using var deleted = await fixture.AdminAsync(HttpMethod.Delete, "organizations/" + fixture.OrganizationId, body: null, cancellation).ConfigureAwait(true);
            var reconciler = new KeycloakTenantReconciler(directory.Store);
            await VerifyReconciliationRecoveryAsync(reconciler, directory, fixture.OrganizationId, cancellation).ConfigureAwait(true);
            (await reconciler.ReconcileAsync([Guid.Parse(fixture.OrganizationId), Guid.Parse(fixture.OrganizationId)], cancellation).ConfigureAwait(true)).ShouldBe(1);
            (await directory.Store.IsActiveAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true)).ShouldBeFalse();
            await ControlBrokerAsync(rabbit, "start_app", cancellation).ConfigureAwait(true);
            var late = Notice("UPDATE", time: 2);
            await PublishAsync(rabbit.GetConnectionString(), Serialize(late), cancellation).ConfigureAwait(true);
            await WaitHandledAsync(database.GetConnectionString(), late, cancellation).ConfigureAwait(true);
            (await directory.Store.IsActiveAsync(fixture.OrganizationId, cancellation).ConfigureAwait(true)).ShouldBeFalse();
        }
        finally
        {
            await host.StopAsync(cancellation).ConfigureAwait(true);
        }
    }

    private static async Task VerifyReconciliationRecoveryAsync(KeycloakTenantReconciler reconciler, KeycloakStoreClient directory, string organizationId, CancellationToken cancellation)
    {
        using var measurements = new ReconciliationMeasurements();
        _ = await Should.ThrowAsync<ArgumentNullException>(() => reconciler.ReconcileAsync(null!, cancellation)).ConfigureAwait(false);
        _ = await Should.ThrowAsync<ArgumentException>(() => reconciler.ReconcileAsync([Guid.Empty], cancellation)).ConfigureAwait(false);
        _ = await Should.ThrowAsync<ArgumentException>(() => reconciler.ReconcileAsync([.. Enumerable.Range(0, 257).Select(_ => Guid.NewGuid())], cancellation)).ConfigureAwait(false);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(false);
        _ = await Should.ThrowAsync<OperationCanceledException>(() => reconciler.ReconcileAsync([Guid.Parse(organizationId)], cancelled.Token)).ConfigureAwait(false);
        directory.Handler.Unavailable = true;
        _ = await Should.ThrowAsync<HttpRequestException>(() => reconciler.ReconcileAsync([Guid.Parse(organizationId)], cancellation)).ConfigureAwait(false);
        directory.Handler.Unavailable = false;
        (await reconciler.ReconcileAsync([], cancellation).ConfigureAwait(false)).ShouldBe(0);
        measurements.VerifyBoundedOutcomes();
    }

    private static byte[] Serialize(KeycloakOrganizationNotice notice) => new KeycloakEventSerializer().WriteMessage(notice);

    private static async Task PublishAsync(string broker, byte[] body, CancellationToken cancellationToken)
    {
        var connection = await new ConnectionFactory { Uri = new Uri(broker) }.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var disposeConnection = connection.ConfigureAwait(false);
        var channel = await connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), cancellationToken).ConfigureAwait(false);
        await using var disposeChannel = channel.ConfigureAwait(false);
        await channel.BasicPublishAsync("identity-events", "identity-events", mandatory: true, new BasicProperties { ContentType = "application/json", Persistent = true }, body, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WaitHandledAsync(string database, KeycloakOrganizationNotice notice, CancellationToken cancellationToken)
    {
        var envelope = new Envelope { Data = Serialize(notice) };
        new KeycloakEventMapper("fixture-issuer", "teck-local", new Uri("rabbitmq://queue/identity-events")).MapIncomingToEnvelope(envelope, new BasicProperties());
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var connection = new NpgsqlConnection(database);
        await using var disposeConnection = connection.ConfigureAwait(false);
        await connection.OpenAsync(deadline.Token).ConfigureAwait(false);
        var command = new NpgsqlCommand("SELECT count(*) FROM identity_events.wolverine_incoming_envelopes WHERE id=@id AND status='Handled'", connection);
        await using var disposeCommand = command.ConfigureAwait(false);
        _ = command.Parameters.AddWithValue("id", envelope.Id);
        while ((long)(await command.ExecuteScalarAsync(deadline.Token).ConfigureAwait(false))! != 1)
        {
            await Task.Delay(40, deadline.Token).ConfigureAwait(false);
        }
    }

    private KeycloakOrganizationNotice Notice(string operation, long time) => new(Guid.NewGuid(), time, "teck-local", "ORGANIZATION", operation, "organizations/" + fixture.OrganizationId);
}
