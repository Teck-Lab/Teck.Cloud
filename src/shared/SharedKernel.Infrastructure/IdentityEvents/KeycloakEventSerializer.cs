using System.Text.Json;
using System.Text.Json.Serialization;
using Wolverine;
using Wolverine.Runtime.Serialization;

namespace SharedKernel.Infrastructure.IdentityEvents;

/// <summary>Source-generated serialization for the deliberately small identity change contract.</summary>
public sealed partial class KeycloakEventSerializer : IMessageSerializer
{
    /// <inheritdoc/>
    public string ContentType => "application/json";

    /// <inheritdoc/>
    public byte[] Write(Envelope envelope) => WriteMessage(envelope.Message!);

    /// <inheritdoc/>
    public object ReadFromData(Type messageType, Envelope envelope) => ReadFromData(envelope.Data!);

    /// <inheritdoc/>
    public object ReadFromData(byte[] data) => JsonSerializer.Deserialize(data, EventJson.Default.KeycloakOrganizationNotice)
        ?? throw new InvalidDataException("Missing identity event metadata.");

    /// <inheritdoc/>
    public byte[] WriteMessage(object message) => message is KeycloakOrganizationNotice notice
        ? JsonSerializer.SerializeToUtf8Bytes(notice, EventJson.Default.KeycloakOrganizationNotice)
        : throw new InvalidDataException("Unsupported identity event type.");

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
    [JsonSerializable(typeof(KeycloakOrganizationNotice))]
    private sealed partial class EventJson : JsonSerializerContext;
}
