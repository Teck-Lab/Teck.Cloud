using ErrorOr;
using SharedKernel.Core.Database;
using Teck.Cloud.Commerce.Orders.Application.Orders.ReadModels;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.GetCustomerOrder.V1;

/// <summary>Customer order detail handler.</summary>
public static class GetCustomerOrderHandler
{
    /// <summary>Reads the scoped order without tracking.</summary>
    /// <param name="query">The query.</param>
    /// <param name="orders">The read-only order repository.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The projection, or a scoped not-found error.</returns>
    public static Task<ErrorOr<CustomerOrderDto>> HandleAsync(
        GetCustomerOrderQuery query,
        IGenericReadRepository<OrderReadModel, Guid> orders,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleCoreAsync(query, orders, clock, cancellationToken);
    }

    private static async Task<ErrorOr<CustomerOrderDto>> HandleCoreAsync(
        GetCustomerOrderQuery query,
        IGenericReadRepository<OrderReadModel, Guid> orders,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var order = await orders.FindOneAsync(
            candidate => candidate.Id == query.OrderId && candidate.SubjectId == query.SubjectId,
            cancellationToken).ConfigureAwait(false);
        return order is null
            ? OrderErrors.NotFound
            : OrderProjection.Customer(OrderProjection.From(order), clock.GetUtcNow());
    }
}
