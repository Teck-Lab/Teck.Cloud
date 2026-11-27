using ErrorOr;
using SharedKernel.Core.Database;
using Teck.Cloud.Commerce.Orders.Application.Orders.ReadModels;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.GetAdminOrder.V1;

/// <summary>Admin order detail handler.</summary>
public static class GetAdminOrderHandler
{
    /// <summary>Reads the scoped order and its audit entries.</summary>
    /// <param name="query">The query.</param>
    /// <param name="orders">The read-only order repository.</param>
    /// <param name="audit">The read-only audit repository.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The projection, or a scoped not-found error.</returns>
    public static Task<ErrorOr<AdminOrderDto>> HandleAsync(
        GetAdminOrderQuery query,
        IGenericReadRepository<OrderReadModel, Guid> orders,
        IGenericReadRepository<OrderAuditReadModel, Guid> audit,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleCoreAsync(query, orders, audit, clock, cancellationToken);
    }

    private static async Task<ErrorOr<AdminOrderDto>> HandleCoreAsync(
        GetAdminOrderQuery query,
        IGenericReadRepository<OrderReadModel, Guid> orders,
        IGenericReadRepository<OrderAuditReadModel, Guid> audit,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var order = await orders.FindByIdAsync(query.OrderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return OrderErrors.NotFound;
        }

        var entries = await audit.FindAsync(entry => entry.OrderId == query.OrderId, cancellationToken).ConfigureAwait(false);
        return OrderProjection.Admin(
            OrderProjection.From(order),
            [.. entries.Select(OrderProjection.Audit)],
            clock.GetUtcNow());
    }
}
