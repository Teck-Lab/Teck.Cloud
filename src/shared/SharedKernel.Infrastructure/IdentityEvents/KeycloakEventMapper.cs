using System.Security.Cryptography;
using System.Text;
using RabbitMQ.Client;
using Wolverine;
using Wolverine.RabbitMQ.Internal;
using Wolverine.Util;

namespace SharedKernel.Infrastructure.IdentityEvents;

/// <summary>Maps only validated metadata into the durable inbox; poison bodies are erased before error storage.</summary>
/// <param name="source">A fixed host-configured provider identity, unique across issuers.</param>
/// <param name="realm">The single permitted source realm.</param>
/// <param name="destination">The configured listener address, needed before durable poison handling.</param>
public sealed class KeycloakEventMapper(string source, string realm, Uri destination) : IRabbitMqEnvelopeMapper
{
    private readonly KeycloakEventSerializer _serializer = new();
    private readonly string _source = ValidateSetting(source);
    private readonly string _realm = ValidateSetting(realm);
    private readonly Uri _destination = destination ?? throw new ArgumentNullException(nameof(destination));

    /// <inheritdoc/>
    public void MapIncomingToEnvelope(Envelope envelope, IReadOnlyBasicProperties incoming)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var bytes = envelope.Data;
        envelope.Data = [];
        envelope.Destination = _destination;
        envelope.TenantId = null;
        envelope.Id = Guid.NewGuid();
        envelope.ContentType = _serializer.ContentType;
        envelope.MessageType = typeof(KeycloakOrganizationNotice).ToMessageTypeName();
        envelope.Headers.Clear();
        if (bytes is null || bytes.Length > 2048)
        {
            throw new InvalidDataException("Rejected identity event metadata.");
        }

        var notice = TryRead(bytes);
        if (notice is null || notice.Id == Guid.Empty || notice.Time <= 0 || !string.Equals(notice.Realm, _realm, StringComparison.Ordinal)
            || !string.Equals(notice.ResourceType, "ORGANIZATION", StringComparison.Ordinal) || notice.Operation is not ("CREATE" or "UPDATE" or "DELETE")
            || notice.ResourcePath?.StartsWith("organizations/", StringComparison.Ordinal) != true
            || !Guid.TryParseExact(notice.ResourcePath[14..], "D", out var organization))
        {
            throw new InvalidDataException("Rejected identity event metadata.");
        }

        notice = notice with { ResourcePath = "organizations/" + organization.ToString("D") };
        var identity = Encoding.UTF8.GetBytes(_source + "\n" + _realm + "\n" + notice.Id.ToString("D"));
        envelope.Id = new Guid(SHA256.HashData(identity).AsSpan(0, 16));
        envelope.Data = _serializer.WriteMessage(notice);
    }

    /// <inheritdoc/>
    public void MapEnvelopeToOutgoing(Envelope envelope, IBasicProperties outgoing)
    {
        outgoing.MessageId = envelope.Id.ToString("D");
        outgoing.ContentType = _serializer.ContentType;
        outgoing.Type = typeof(KeycloakOrganizationNotice).ToMessageTypeName();
    }

    private static string ValidateSetting(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 256
        ? value
        : throw new ArgumentException("A bounded trusted identity source and realm are required.", nameof(value));

    private KeycloakOrganizationNotice? TryRead(byte[] bytes)
    {
        try
        {
            return (KeycloakOrganizationNotice)_serializer.ReadFromData(bytes);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
