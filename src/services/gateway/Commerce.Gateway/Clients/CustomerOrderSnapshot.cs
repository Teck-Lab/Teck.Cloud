using Teck.Cloud.Commerce.Gateway.Models;

namespace Teck.Cloud.Commerce.Gateway.Clients;

/// <summary>An internal Orders snapshot carrying the desired generation that is never serialized.</summary>
/// <param name="OrderId">Order identifier.</param>
/// <param name="SubscriptionId">Subscription identifier.</param>
/// <param name="Tier">Effective database tier.</param>
/// <param name="AddOnCodes">Purchased add-ons.</param>
/// <param name="Total">Immutable total.</param>
/// <param name="Status">Effective customer status.</param>
/// <param name="Revision">Order revision.</param>
/// <param name="CreatedAt">Creation instant.</param>
/// <param name="UpdatedAt">Last update instant.</param>
/// <param name="DesiredRevision">Internal desired entitlement generation; positive only when active.</param>
/// <param name="License">License already confirmed by the gateway, if any.</param>
public sealed record CustomerOrderSnapshot(
    Guid OrderId,
    Guid SubscriptionId,
    string Tier,
    IReadOnlyList<string> AddOnCodes,
    MoneyView Total,
    string Status,
    uint Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint? DesiredRevision,
    LicenseView? License);
