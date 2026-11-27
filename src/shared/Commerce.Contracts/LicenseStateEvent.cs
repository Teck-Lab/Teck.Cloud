using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using SharedKernel.Core.Events;
using SharedKernel.Core.MultiTenancy;

namespace Teck.Cloud.Commerce.Contracts;

/// <summary>Licensing-to-Orders grant outcome (commerce.license-state.v1).</summary>
/// <param name="MessageId">Stable event identity retained across transport retries.</param>
/// <param name="OccurredAt">RFC 3339 UTC instant.</param>
/// <param name="TenantId">Immutable Keycloak organization UUID.</param>
/// <param name="OrderId">The owning order.</param>
/// <param name="SubscriptionId">The owning subscription.</param>
/// <param name="Revision">Desired entitlement generation answered by this outcome; equals <paramref name="DesiredRevision"/>.</param>
/// <param name="DesiredRevision">Echo of the answered desired generation.</param>
/// <param name="State"><see cref="CommerceMessaging.ActiveState"/> or <see cref="CommerceMessaging.InactiveState"/>.</param>
/// <param name="LicenseRevision">Monotonic license revision for active outcomes; null when inactive.</param>
/// <param name="KeyId">Opaque signing key identifier for active outcomes; null when inactive.</param>
/// <param name="ExpiresAt">License expiry for active outcomes; null when inactive.</param>
public sealed record LicenseStateEvent(
    [property: JsonPropertyName("messageId")] Guid MessageId,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("tenantId")] Guid TenantId,
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("subscriptionId")] Guid SubscriptionId,
    [property: JsonPropertyName("revision")] uint Revision,
    [property: JsonPropertyName("desiredRevision")] uint DesiredRevision,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("licenseRevision")] uint? LicenseRevision,
    [property: JsonPropertyName("keyId")] string? KeyId,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset? ExpiresAt) : IIntegrationEvent, ITenantMessage
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
