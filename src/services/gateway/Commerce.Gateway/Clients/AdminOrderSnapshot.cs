using Teck.Cloud.Commerce.Gateway.Models;

namespace Teck.Cloud.Commerce.Gateway.Clients;

/// <summary>A staff order snapshot without license, provider or secret fields.</summary>
/// <param name="OrderId">Order identifier.</param>
/// <param name="SubscriptionId">Subscription identifier.</param>
/// <param name="Tier">Effective database tier.</param>
/// <param name="AddOnCodes">Purchased add-ons.</param>
/// <param name="Total">Immutable total.</param>
/// <param name="Status">Effective lifecycle status.</param>
/// <param name="Revision">Order revision.</param>
/// <param name="CreatedAt">Creation instant.</param>
/// <param name="UpdatedAt">Last update instant.</param>
/// <param name="Payment">Verified payment facts.</param>
/// <param name="Approval">Decision facts.</param>
/// <param name="Fulfillment">Placement verification facts.</param>
/// <param name="Audit">Recorded staff actions.</param>
public sealed record AdminOrderSnapshot(
    Guid OrderId,
    Guid SubscriptionId,
    string Tier,
    IReadOnlyList<string> AddOnCodes,
    MoneyView Total,
    string Status,
    uint Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    PaymentView Payment,
    ApprovalView Approval,
    FulfillmentView Fulfillment,
    IReadOnlyList<AuditEntryView> Audit);
