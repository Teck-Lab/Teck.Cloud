using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using RabbitMQ.Client;
using SharedKernel.Infrastructure.IdentityEvents;
using Shouldly;
using Testcontainers.RabbitMq;
using Wolverine;
using Xunit;

namespace SharedKernel.Infrastructure.IntegrationTests;

/// <summary>Explicit privacy-gated evaluation; requires the separate checksum-pinned KETE image.</summary>
public sealed class KeycloakKeteEvaluationTests
{
    /// <summary>Checks actual organization publication and the upstream database-retention side effect.</summary>
    /// <returns>The isolated verification task.</returns>
    [Fact(Explicit = true)]
    public async Task NativeOrganizationEventUsesSanitizedTemplateButEnablesAdminRepresentations()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var password = RandomNumberGenerator.GetHexString(48);
        await using var fixture = new KeycloakFixture("teck/keycloak-kete:26.8.2-evaluation", Settings(password));
        await fixture.Network.CreateAsync(cancellation).ConfigureAwait(true);
        await using var rabbit = new RabbitMqBuilder("rabbitmq:4-alpine").WithNetwork(fixture.Network).WithNetworkAliases("identity-rabbit").WithUsername("fixture-admin").WithPassword(RandomNumberGenerator.GetHexString(48)).Build();
        await rabbit.StartAsync(cancellation).ConfigureAwait(true);
        await using var connection = await new ConnectionFactory { Uri = new Uri(rabbit.GetConnectionString()) }.CreateConnectionAsync(cancellation).ConfigureAwait(true);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellation).ConfigureAwait(true);
        await channel.ExchangeDeclareAsync("identity-events", ExchangeType.Fanout, durable: true, cancellationToken: cancellation).ConfigureAwait(true);
        _ = await channel.QueueDeclareAsync("identity-evaluation", durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellation).ConfigureAwait(true);
        await channel.QueueBindAsync("identity-evaluation", "identity-events", "identity-events", cancellationToken: cancellation).ConfigureAwait(true);
        await ConfigurePublisherAsync(rabbit, password, cancellation).ConfigureAwait(true);
        await fixture.InitializeAsync().ConfigureAwait(true);
        using var updated = await fixture.UpdateOrganizationAsync(new JsonObject { ["enabled"] = false }, cancellation).ConfigureAwait(true);
        var body = await ReadUpdateAsync(channel, fixture.OrganizationId, cancellation).ConfigureAwait(true);
        using var document = JsonDocument.Parse(body);
        document.RootElement.EnumerateObject().Count().ShouldBe(6);
        var envelope = new Envelope { Data = body };
        new KeycloakEventMapper("fixture-issuer", "teck-local", new Uri("rabbitmq://queue/identity-evaluation")).MapIncomingToEnvelope(envelope, new BasicProperties());
        var notice = (KeycloakOrganizationNotice)new KeycloakEventSerializer().ReadFromData(envelope.Data);
        notice.Id.ShouldNotBe(Guid.Empty);
        notice.Time.ShouldBeGreaterThan(0);
        notice.ResourcePath.ShouldBe("organizations/" + fixture.OrganizationId);
        using var realm = await fixture.AdminAsync(HttpMethod.Get, string.Empty, body: null, cancellation).ConfigureAwait(true);
        realm.RootElement.GetProperty("eventsEnabled").GetBoolean().ShouldBeTrue();
        realm.RootElement.GetProperty("adminEventsEnabled").GetBoolean().ShouldBeTrue();
        realm.RootElement.GetProperty("adminEventsDetailsEnabled").GetBoolean().ShouldBeTrue();
    }

    private static async Task ConfigurePublisherAsync(RabbitMqContainer rabbit, string password, CancellationToken cancellationToken)
    {
        var created = await rabbit.ExecAsync(["rabbitmqctl", "add_user", "identity-publisher", password], cancellationToken).ConfigureAwait(false);
        created.ExitCode.ShouldBe(0);
        var permissions = await rabbit.ExecAsync(["rabbitmqctl", "set_permissions", "-p", "/", "identity-publisher", "^$", "^identity-events$", "^$"], cancellationToken).ConfigureAwait(false);
        permissions.ExitCode.ShouldBe(0);
    }

    private static Dictionary<string, string> Settings(string password) => new(StringComparer.Ordinal)
    {
        ["kete.enabled"] = "true",
        ["kete.routes.tenant.realm-matchers.realm"] = "list:teck-local",
        ["kete.routes.tenant.event-matchers.filter"] = "list:ORGANIZATION_CREATE,ORGANIZATION_UPDATE,ORGANIZATION_DELETE",
        ["kete.routes.tenant.serializer.kind"] = "template",
        ["kete.routes.tenant.serializer.variable-escape"] = "json",
        ["kete.routes.tenant.serializer.content-type"] = "application/json",
        ["kete.routes.tenant.serializer.template-file-text"] = """{"id":"${id}","time":${time:none},"realm":"${realmName}","resourceType":"${resourceType}","operation":"${operationType}","resourcePath":"${resourcePath}"}""",
        ["kete.routes.tenant.destination.kind"] = "amqp-0.9.1",
        ["kete.routes.tenant.destination.host"] = "identity-rabbit",
        ["kete.routes.tenant.destination.username"] = "identity-publisher",
        ["kete.routes.tenant.destination.password"] = password,
        ["kete.routes.tenant.destination.exchange"] = "identity-events",
        ["kete.routes.tenant.destination.routing-key"] = "identity-events",
        ["kete.routes.tenant.destination.publisher-confirms"] = "true",
    };

    private static async Task<byte[]> ReadUpdateAsync(IChannel channel, string organization, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        while (true)
        {
            var delivery = await channel.BasicGetAsync("identity-evaluation", autoAck: true, deadline.Token).ConfigureAwait(false);
            if (delivery is not null)
            {
                using var document = JsonDocument.Parse(delivery.Body);
                if (string.Equals(document.RootElement.GetProperty("operation").GetString(), "UPDATE", StringComparison.Ordinal)
                    && string.Equals(document.RootElement.GetProperty("resourcePath").GetString(), "organizations/" + organization, StringComparison.Ordinal))
                {
                    return delivery.Body.ToArray();
                }
            }

            await Task.Delay(40, deadline.Token).ConfigureAwait(false);
        }
    }
}
