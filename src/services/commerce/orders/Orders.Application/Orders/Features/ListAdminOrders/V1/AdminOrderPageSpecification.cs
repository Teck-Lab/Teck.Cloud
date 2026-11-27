using Ardalis.Specification;
using Teck.Cloud.Commerce.Orders.Application.Orders.Cursor;
using Teck.Cloud.Commerce.Orders.Application.Orders.ReadModels;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.ListAdminOrders.V1;

/// <summary>Cursor-ordered tenant-scoped admin page.</summary>
public sealed class AdminOrderPageSpecification : Specification<OrderReadModel>
{
    /// <summary>Initializes a new instance of the <see cref="AdminOrderPageSpecification"/> class.</summary>
    /// <param name="after">The exclusive cursor position, or null for the first page.</param>
    public AdminOrderPageSpecification(OrderCursorPosition? after)
    {
        if (after is { } position)
        {
            _ = Query.Where(order => (order.UpdatedOn ?? order.CreatedAt) < position.UpdatedAt
                || ((order.UpdatedOn ?? order.CreatedAt) == position.UpdatedAt && order.SortKey.CompareTo(position.SortKey) > 0));
        }

        _ = Query.OrderByDescending(order => order.UpdatedOn ?? order.CreatedAt)
            .ThenBy(order => order.SortKey)
            .Take(101);
    }
}
