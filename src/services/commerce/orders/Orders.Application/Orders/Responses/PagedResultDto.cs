namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>A fixed-membership page.</summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">The page items.</param>
/// <param name="NextCursor">The opaque next cursor, or null on the final page.</param>
public sealed record PagedResultDto<T>(IReadOnlyList<T> Items, string? NextCursor);
