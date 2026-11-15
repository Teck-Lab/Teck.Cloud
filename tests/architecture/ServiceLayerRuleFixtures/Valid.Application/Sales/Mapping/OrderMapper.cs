using ArchitectureRuleFixtures.Valid.Domain.Aggregates.Orders;
using Riok.Mapperly.Abstractions;

namespace ArchitectureRuleFixtures.Valid.Application.Sales.Mapping;

/// <summary>Generated mapping from the order aggregate to an application DTO.</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class OrderMapper
{
    /// <summary>Maps an order to its application-facing summary.</summary>
    /// <param name="order">The source order.</param>
    /// <returns>The summary DTO.</returns>
    public static partial OrderSummary ToSummary(Order order);
}
