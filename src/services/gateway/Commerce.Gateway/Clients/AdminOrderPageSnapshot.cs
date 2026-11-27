namespace Teck.Cloud.Commerce.Gateway.Clients;

/// <summary>A bounded page of staff order snapshots.</summary>
/// <param name="Items">Snapshots in Orders order.</param>
/// <param name="NextCursor">Original opaque cursor, or null on the last page.</param>
public sealed record AdminOrderPageSnapshot(IReadOnlyList<AdminOrderSnapshot> Items, string? NextCursor);
