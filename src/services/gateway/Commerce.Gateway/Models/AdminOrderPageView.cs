namespace Teck.Cloud.Commerce.Gateway.Models;

/// <summary>A bounded page of one selected tenant's orders.</summary>
/// <param name="Items">Projections in deterministic order.</param>
/// <param name="NextCursor">Original opaque Orders cursor, or null on the last page.</param>
public sealed record AdminOrderPageView(IReadOnlyList<AdminOrderView> Items, string? NextCursor);
