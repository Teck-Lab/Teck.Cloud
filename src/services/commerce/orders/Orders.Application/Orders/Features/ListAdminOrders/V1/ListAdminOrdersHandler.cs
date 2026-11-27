using ErrorOr;
using SharedKernel.Core.Database;
using Teck.Cloud.Commerce.Orders.Application.Orders.Cursor;
using Teck.Cloud.Commerce.Orders.Application.Orders.ReadModels;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.ListAdminOrders.V1;

/// <summary>Admin order list handler.</summary>
public static class ListAdminOrdersHandler
{
    /// <summary>Reads one fixed page with its audit entries.</summary>
    /// <param name="query">The query.</param>
    /// <param name="orders">The read-only order repository.</param>
    /// <param name="audit">The read-only audit repository.</param>
    /// <param name="cursors">The scoped cursor codec.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The page, or a stable error.</returns>
    public static Task<ErrorOr<PagedResultDto<AdminOrderDto>>> HandleAsync(
        ListAdminOrdersQuery query,
        IGenericReadRepository<OrderReadModel, Guid> orders,
        IGenericReadRepository<OrderAuditReadModel, Guid> audit,
        IOrderCursorCodec cursors,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleCoreAsync(query, orders, audit, cursors, clock, cancellationToken);
    }

    private static async Task<ErrorOr<PagedResultDto<AdminOrderDto>>> HandleCoreAsync(
        ListAdminOrdersQuery query,
        IGenericReadRepository<OrderReadModel, Guid> orders,
        IGenericReadRepository<OrderAuditReadModel, Guid> audit,
        IOrderCursorCodec cursors,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var limit = query.Limit == 0 ? 25 : query.Limit;
        if (limit > 100)
        {
            return Error.Validation("invalid_request", "The limit must not exceed 100.");
        }

        var scope = new OrderCursorScope(OrderListSurface.Admin, query.TenantId, SubjectId: null);
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

        var page = await orders.ListAsync(new AdminOrderPageSpecification(after), cancellationToken).ConfigureAwait(false);
        var hasMore = page.Count > limit;
        var items = page.Take((int)limit).ToArray();
        var ids = items.Select(static item => item.Id).ToArray();
        var entries = ids.Length == 0
            ? []
            : await audit.FindAsync(entry => ids.Contains(entry.OrderId), cancellationToken).ConfigureAwait(false);
        var byOrder = entries.ToLookup(static entry => entry.OrderId);
        var now = clock.GetUtcNow();
        var projected = items
            .Select(item => OrderProjection.Admin(
                OrderProjection.From(item),
                [.. byOrder[item.Id].Select(OrderProjection.Audit)],
                now))
            .ToArray();
        var next = hasMore && items.Length > 0
            ? cursors.Encode(scope, new OrderCursorPosition(items[^1].UpdatedOn ?? items[^1].CreatedAt, items[^1].SortKey, items[^1].Id))
            : null;
        return new PagedResultDto<AdminOrderDto>(projected, next);
    }
}
