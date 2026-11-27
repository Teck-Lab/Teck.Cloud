namespace Teck.Cloud.Commerce.Gateway.Clients;

/// <summary>A fixed-membership page of internal Orders snapshots.</summary>
/// <param name="Items">Snapshots in Orders order.</param>
/// <param name="NextCursor">Original opaque cursor, or null on the last page.</param>
public sealed record CustomerOrderPageSnapshot(IReadOnlyList<CustomerOrderSnapshot> Items, string? NextCursor);
