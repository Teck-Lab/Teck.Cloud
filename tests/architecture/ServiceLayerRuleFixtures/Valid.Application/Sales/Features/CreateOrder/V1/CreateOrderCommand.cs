using SharedKernel.Core.CQRS;

namespace ArchitectureRuleFixtures.Valid.Application.Sales.Features.CreateOrder.V1;

/// <summary>Immutable command colocated with its handler in the positive fixture.</summary>
/// <param name="OrderId">The identifier of the order to create.</param>
public sealed record CreateOrderCommand(Guid OrderId) : ICommand<Guid>;
