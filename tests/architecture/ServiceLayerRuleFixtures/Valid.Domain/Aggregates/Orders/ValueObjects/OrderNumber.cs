namespace ArchitectureRuleFixtures.Valid.Domain.Aggregates.Orders.ValueObjects;

/// <summary>A value object nested under its owning aggregate namespace.</summary>
/// <param name="Value">The fixture order number value.</param>
public readonly record struct OrderNumber(string Value);
