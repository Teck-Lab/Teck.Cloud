using ErrorOr;
using SharedKernel.Core.Database;
using Teck.Cloud.Commerce.Orders.Application.Orders.Cursor;
using Teck.Cloud.Commerce.Orders.Application.Orders.ReadModels;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.ListCustomerOrders.V1;

/// <summary>Customer order list handler.</summary>
public static class ListCustomerOrdersHandler
{
    /// <summary>Reads one fixed page.</summary>
    /// <param name="query">The query.</param>
    /// <param name="orders">The read-only order repository.</param>
    /// <param name="cursors">The scoped cursor codec.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The page, or a stable error.</returns>
    public static Task<ErrorOr<PagedResultDto<CustomerOrderDto>>> HandleAsync(
        ListCustomerOrdersQuery query,
        IGenericReadRepository<OrderReadModel, Guid> orders,
        IOrderCursorCodec cursors,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleCoreAsync(query, orders, cursors, clock, cancellationToken);
    }

    private static async Task<ErrorOr<PagedResultDto<CustomerOrderDto>>> HandleCoreAsync(
        ListCustomerOrdersQuery query,
        IGenericReadRepository<OrderReadModel, Guid> orders,
        IOrderCursorCodec cursors,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var limit = query.Limit == 0 ? 25 : query.Limit;
        if (limit > 100)
        {
            return Error.Validation("invalid_request", "The limit must not exceed 100.");
        }

        var scope = new OrderCursorScope(OrderListSurface.Customer, query.TenantId, query.SubjectId);
        OrderCursorPosition? after = null;
        if (!string.IsNullOrEmpty(query.Cursor))
        {
            var decoded = cursors.Decode(scope, query.Cursor);
            if (decoded.IsError)
            {
                return decoded.FirstError;
            }

            after = decoded.Value;
        }

        var page = await orders.ListAsync(
            new CustomerOrderPageSpecification(query.SubjectId, after),
            cancellationToken).ConfigureAwait(false);
        var hasMore = page.Count > limit;
        var items = page.Take((int)limit).ToArray();
        var now = clock.GetUtcNow();
        var projected = items.Select(item => OrderProjection.Customer(OrderProjection.From(item), now)).ToArray();
        var next = hasMore && items.Length > 0
            ? cursors.Encode(scope, new OrderCursorPosition(items[^1].UpdatedOn ?? items[^1].CreatedAt, items[^1].SortKey, items[^1].Id))
            : null;
        return new PagedResultDto<CustomerOrderDto>(projected, next);
    }
}
