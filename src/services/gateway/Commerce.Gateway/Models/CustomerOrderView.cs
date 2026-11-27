namespace Teck.Cloud.Commerce.Gateway.Models;

/// <summary>Customer-visible order facts.</summary>
/// <param name="OrderId">Order identifier.</param>
/// <param name="SubscriptionId">Subscription identifier.</param>
/// <param name="Tier">Effective database tier.</param>
/// <param name="AddOnCodes">Purchased add-ons.</param>
/// <param name="Total">Immutable server-owned total.</param>
/// <param name="Status">Effective customer status.</param>
/// <param name="Revision">Order revision, not the desired generation.</param>
/// <param name="CreatedAt">Creation instant.</param>
/// <param name="UpdatedAt">Last update instant.</param>
/// <param name="License">Confirmed license envelope only when active.</param>
public sealed record CustomerOrderView(
    Guid OrderId,
    Guid SubscriptionId,
    string Tier,
    IReadOnlyList<string> AddOnCodes,
    MoneyView Total,
    string Status,
    uint Revision,
    string CreatedAt,
    string UpdatedAt,
    LicenseView? License);
