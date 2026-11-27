using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using SharedKernel.Core.Events;
using SharedKernel.Core.MultiTenancy;

namespace Teck.Cloud.Commerce.Contracts;

/// <summary>Orders-to-Licensing desired entitlement snapshot (commerce.license-desired.v1).</summary>
/// <param name="MessageId">Stable event identity retained across transport retries.</param>
/// <param name="OccurredAt">RFC 3339 UTC instant.</param>
/// <param name="TenantId">Immutable Keycloak organization UUID.</param>
/// <param name="OrderId">The owning order.</param>
/// <param name="SubscriptionId">The owning subscription.</param>
/// <param name="Revision">Desired entitlement generation this message is about.</param>
/// <param name="Enabled">Whether effective entitlement is currently desired.</param>
/// <param name="Tier">Selected database tier, <see cref="CommerceMessaging.SharedTier"/> or <see cref="CommerceMessaging.DedicatedTier"/>.</param>
/// <param name="AddOnCodes">Canonical sorted and deduplicated add-on codes.</param>
/// <param name="NotAfter">Upper bound for the issued license; null when disabled.</param>
[SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "The frozen v1 event contract requires a repeated string field.")]
[SuppressMessage("Usage", "CA2227:Collection properties should be read-only", Justification = "The frozen v1 payload binds through the record constructor.")]
public sealed record LicenseDesiredEvent(
    [property: JsonPropertyName("messageId")] Guid MessageId,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("tenantId")] Guid TenantId,
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("subscriptionId")] Guid SubscriptionId,
    [property: JsonPropertyName("revision")] uint Revision,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("tier")] string Tier,
    [property: JsonPropertyName("addOnCodes")] string[] AddOnCodes,
    [property: JsonPropertyName("notAfter")] DateTimeOffset? NotAfter) : IIntegrationEvent, ITenantMessage
{
    private static readonly IDictionary<string, string> NoMetadata =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

    /// <inheritdoc/>
    string ITenantMessage.TenantId => TenantId.ToString("D");

    /// <inheritdoc/>
    Guid IEvent.Id => MessageId;

    /// <inheritdoc/>
    DateTime IEvent.CreatedOn => OccurredAt.UtcDateTime;

    /// <inheritdoc/>
    IDictionary<string, string> IEvent.MetaData => NoMetadata;
}
