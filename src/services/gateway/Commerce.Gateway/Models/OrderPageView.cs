namespace Teck.Cloud.Commerce.Gateway.Models;

/// <summary>A fixed-membership page of customer orders.</summary>
/// <param name="Items">Projections in deterministic order.</param>
/// <param name="NextCursor">Original opaque Orders cursor, or null on the last page.</param>
public sealed record OrderPageView(IReadOnlyList<CustomerOrderView> Items, string? NextCursor);
