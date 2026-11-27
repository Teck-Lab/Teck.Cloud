namespace Teck.Cloud.Commerce.Orders.Application.Orders.Cursor;

/// <summary>A decoded page position.</summary>
/// <param name="UpdatedAt">The last item's update instant.</param>
/// <param name="SortKey">The last item's canonical order-id sort key.</param>
/// <param name="OrderId">The last item's order id.</param>
public readonly record struct OrderCursorPosition(DateTimeOffset UpdatedAt, string SortKey, Guid OrderId);
