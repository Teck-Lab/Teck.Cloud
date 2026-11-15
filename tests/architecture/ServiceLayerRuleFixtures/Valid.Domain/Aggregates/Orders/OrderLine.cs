namespace ArchitectureRuleFixtures.Valid.Domain.Aggregates.Orders;

/// <summary>A child entity kept with its owning aggregate.</summary>
public sealed class OrderLine
{
    /// <summary>Gets the fixture line quantity.</summary>
    public int Quantity { get; } = 1;
}
