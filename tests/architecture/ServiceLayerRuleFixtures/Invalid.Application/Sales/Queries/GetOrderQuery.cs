using SharedKernel.Core.CQRS;

namespace ArchitectureRuleFixtures.Invalid.Application.Sales.Queries;

/// <summary>A query deliberately placed outside a versioned feature slice.</summary>
/// <param name="OrderId">The identifier of the queried order.</param>
public sealed record GetOrderQuery(Guid OrderId) : IQuery<Guid>;
