using ArchitectureRuleFixtures.Valid.Application.Sales.Features.CreateOrder.V1;
using ArchitectureRuleFixtures.Valid.Domain.Aggregates.Orders;
using ArchitectureRuleFixtures.Valid.Infrastructure.Database;

namespace ArchitectureRuleFixtures.Valid.Host;

/// <summary>Composition type that may reference all three lower service layers.</summary>
public sealed class ServiceComposition
{
    /// <summary>Initializes a new instance of the <see cref="ServiceComposition"/> class.</summary>
    /// <param name="command">An application use case contract.</param>
    /// <param name="order">A domain aggregate.</param>
    /// <param name="store">An infrastructure adapter.</param>
    public ServiceComposition(CreateOrderCommand command, Order order, OrderStore store)
    {
        _ = command;
        _ = order;
        _ = store;
    }
}
