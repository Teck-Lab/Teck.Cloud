using ErrorOr;
using SharedKernel.Core.Database;
using Teck.Cloud.Commerce.Orders.Application.Orders.ReadModels;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;
using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.CheckCurrentEntitlement.V1;

/// <summary>Entitlement check handler.</summary>
public static class CheckCurrentEntitlementHandler
{
    /// <summary>Checks binding, generation and every gate at the current UTC instant.</summary>
    /// <param name="query">The query.</param>
    /// <param name="orders">The read-only order repository.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>Eligibility; ineligible is a successful false result.</returns>
    public static Task<ErrorOr<EntitlementCheckDto>> HandleAsync(
        CheckCurrentEntitlementQuery query,
        IGenericReadRepository<OrderReadModel, Guid> orders,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return EvaluateAsync(query, orders, clock, cancellationToken);
    }

    private static async Task<ErrorOr<EntitlementCheckDto>> EvaluateAsync(
        CheckCurrentEntitlementQuery query,
        IGenericReadRepository<OrderReadModel, Guid> orders,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var order = await orders.FindByIdAsync(query.OrderId, cancellationToken).ConfigureAwait(false);
        if (order is null || order.SubscriptionId != query.SubscriptionId)
        {
            return new EntitlementCheckDto(Eligible: false);
        }

        var view = OrderProjection.From(order);
        var eligible = query.DesiredRevision > 0
            && order.DesiredRevision == query.DesiredRevision
            && view.Status(clock.GetUtcNow()) == OrderStatus.Active;
        return new EntitlementCheckDto(eligible);
    }
}
