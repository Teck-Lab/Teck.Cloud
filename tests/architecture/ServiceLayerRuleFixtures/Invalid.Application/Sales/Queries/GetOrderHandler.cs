namespace ArchitectureRuleFixtures.Invalid.Application.Sales.Queries;

/// <summary>A query handler deliberately placed outside a versioned feature slice.</summary>
public static class GetOrderHandler
{
    /// <summary>Handles a query from the invalid fixture.</summary>
    /// <param name="query">The order query.</param>
    /// <returns>The requested order identifier.</returns>
    public static Guid Handle(GetOrderQuery query) => query.OrderId;
}
