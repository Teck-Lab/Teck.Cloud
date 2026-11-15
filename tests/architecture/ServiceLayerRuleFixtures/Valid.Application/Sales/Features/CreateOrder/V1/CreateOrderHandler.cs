using ArchitectureRuleFixtures.Valid.Domain.Aggregates.Orders;
using SharedKernel.Core.Database;

namespace ArchitectureRuleFixtures.Valid.Application.Sales.Features.CreateOrder.V1;

/// <summary>Wolverine handler colocated with its command in a capability feature slice.</summary>
public static class CreateOrderHandler
{
    /// <summary>Handles the create-order command.</summary>
    /// <param name="command">The request for a new order.</param>
    /// <param name="repository">The write-side order repository.</param>
    /// <returns>The identifier of the requested order.</returns>
    public static Task<Guid> HandleAsync(CreateOrderCommand command, IGenericWriteRepository<Order, Guid> repository)
    {
        _ = repository;
        return Task.FromResult(command.OrderId);
    }
}
