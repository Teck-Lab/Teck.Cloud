using ArchitectureRuleFixtures.Valid.Application.Sales.Features.CreateOrder.V1;
using ArchitectureRuleFixtures.Valid.Domain.Aggregates.Orders;

namespace ArchitectureRuleFixtures.Valid.Infrastructure.Database;

/// <summary>An adapter that references its inward service contracts and domain model.</summary>
public sealed class OrderStore
{
    /// <summary>Initializes a new instance of the <see cref="OrderStore"/> class.</summary>
    /// <param name="command">The application contract handled by the adapter.</param>
    /// <param name="order">The domain aggregate mapped by the adapter.</param>
    public OrderStore(CreateOrderCommand command, Order order)
    {
        _ = command;
        _ = order;
    }
}
