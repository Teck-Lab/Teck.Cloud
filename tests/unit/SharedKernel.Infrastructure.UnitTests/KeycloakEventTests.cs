using System.Text;
using RabbitMQ.Client;
using SharedKernel.Infrastructure.IdentityEvents;
using Shouldly;
using Wolverine;
using Xunit;

namespace SharedKernel.Infrastructure.UnitTests;

/// <summary>Identity event ingress admits only a bounded sanitized contract.</summary>
public sealed class KeycloakEventTests
{
    /// <summary>Retries share a stable provider-scoped inbox ID and ignore caller-selected envelope metadata.</summary>
    [Fact]
    public void DuplicateIdentityIsStableAndSourceScoped()
    {
        var notice = Notice();
        var serializer = new KeycloakEventSerializer();
        var first = new Envelope { Data = serializer.WriteMessage(notice) };
        var duplicate = new Envelope { Data = serializer.WriteMessage(notice) };
        var other = new Envelope { Data = serializer.WriteMessage(notice) };
        var properties = new BasicProperties { Type = "arbitrary.handler", MessageId = Guid.NewGuid().ToString("D") };
        first.Headers["tenant-id"] = "forged";
        new KeycloakEventMapper("issuer-a", "teck", new Uri("rabbitmq://queue/identity-events")).MapIncomingToEnvelope(first, properties);
        new KeycloakEventMapper("issuer-a", "teck", new Uri("rabbitmq://queue/identity-events")).MapIncomingToEnvelope(duplicate, properties);
        new KeycloakEventMapper("issuer-b", "teck", new Uri("rabbitmq://queue/identity-events")).MapIncomingToEnvelope(other, properties);
        first.Id.ShouldBe(duplicate.Id);
        first.Id.ShouldNotBe(other.Id);
        first.Headers.ShouldBeEmpty();
        first.TenantId.ShouldBeNull();
        first.MessageType.ShouldNotBeNull().ShouldContain(nameof(KeycloakOrganizationNotice), Case.Sensitive);
        serializer.ReadFromData(first.Data).ShouldBe(notice);
    }

    /// <summary>Rejected bodies and parser details cannot enter Wolverine's poison-message storage.</summary>
    /// <param name="body">Malformed or excess payload.</param>
    [Theory]
    [InlineData("{secret-not-json")]
    [InlineData("{\"representation\":\"secret-value\"}")]
    [InlineData("null")]
    public void PoisonPayloadIsErasedBeforeFailure(string body)
    {
        var envelope = new Envelope { Data = Encoding.UTF8.GetBytes(body) };
        var error = Should.Throw<InvalidDataException>(() => new KeycloakEventMapper("issuer", "teck", new Uri("rabbitmq://queue/identity-events")).MapIncomingToEnvelope(envelope, new BasicProperties()));
        envelope.Data.ShouldBeEmpty();
        error.ToString().ShouldNotContain("secret", Case.Insensitive);
        error.InnerException.ShouldBeNull();
    }

    /// <summary>Wrong realms, types, operations and non-UUID paths cannot cause a tenant refresh.</summary>
    [Fact]
    public void RejectsUntrustedMetadataAndOversizedBodies()
    {
        var serializer = new KeycloakEventSerializer();
        var expected = Notice();
        var rejected = new[]
        {
            expected with { Id = Guid.Empty }, expected with { Time = 0 }, expected with { Realm = "another" },
            expected with { ResourceType = "USER" }, expected with { Operation = "ACTION" },
            expected with { ResourcePath = null! }, expected with { ResourcePath = "wrong-prefix" }, expected with { ResourcePath = "organizations/alias" }, expected with { ResourcePath = "organizations/../master" },
        };
        foreach (var notice in rejected)
        {
            var envelope = new Envelope { Data = serializer.WriteMessage(notice) };
            _ = Should.Throw<InvalidDataException>(() => new KeycloakEventMapper("issuer", "teck", new Uri("rabbitmq://queue/identity-events")).MapIncomingToEnvelope(envelope, new BasicProperties()));
            envelope.Data.ShouldBeEmpty();
        }

        var oversized = new Envelope { Data = new byte[2049] };
        _ = Should.Throw<InvalidDataException>(() => new KeycloakEventMapper("issuer", "teck", new Uri("rabbitmq://queue/identity-events")).MapIncomingToEnvelope(oversized, new BasicProperties()));
        oversized.Data.ShouldBeEmpty();
    }

    /// <summary>Retry serialization preserves only the fixed contract and native durable identity.</summary>
    [Fact]
    public void SerializerAndOutgoingMappingPreserveSanitizedContract()
    {
        var serializer = new KeycloakEventSerializer();
        var notice = Notice();
        var envelope = new Envelope { Id = Guid.NewGuid(), Message = notice };
        envelope.Data = serializer.Write(envelope);
        serializer.ReadFromData(typeof(KeycloakOrganizationNotice), envelope).ShouldBe(notice);
        var properties = new BasicProperties();
        new KeycloakEventMapper("issuer", "teck", new Uri("rabbitmq://queue/identity-events")).MapEnvelopeToOutgoing(envelope, properties);
        properties.MessageId.ShouldBe(envelope.Id.ToString("D"));
        properties.ContentType.ShouldBe("application/json");
        properties.Type.ShouldNotBeNull().ShouldContain(nameof(KeycloakOrganizationNotice), Case.Sensitive);
        _ = Should.Throw<InvalidDataException>(() => serializer.WriteMessage("unsupported"));
        _ = Should.Throw<InvalidDataException>(() => serializer.ReadFromData("null"u8.ToArray()));
    }

    /// <summary>Host configuration and absent input fail before any provider access.</summary>
    [Fact]
    public void ConfigurationAndMissingBodiesAreRejected()
    {
        _ = Should.Throw<ArgumentException>(() => new KeycloakEventMapper(" ", "teck", new Uri("rabbitmq://queue/identity-events")));
        _ = Should.Throw<ArgumentException>(() => new KeycloakEventMapper("issuer", new string('a', 257), new Uri("rabbitmq://queue/identity-events")));
        _ = Should.Throw<ArgumentNullException>(() => new KeycloakEventMapper("issuer", "teck", null!));
        var mapper = new KeycloakEventMapper("issuer", "teck", new Uri("rabbitmq://queue/identity-events"));
        _ = Should.Throw<ArgumentNullException>(() => mapper.MapIncomingToEnvelope(null!, new BasicProperties()));
        var envelope = new Envelope { Data = [] };
        _ = Should.Throw<InvalidDataException>(() => mapper.MapIncomingToEnvelope(envelope, new BasicProperties()));
        envelope.Data.ShouldBeEmpty();
    }

    private static KeycloakOrganizationNotice Notice() => new(Guid.NewGuid(), 1, "teck", "ORGANIZATION", "UPDATE", "organizations/" + Guid.NewGuid().ToString("D"));
}
