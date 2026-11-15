// The Common namespace follows the optional cross-aggregate folder.
namespace ArchitectureRuleFixtures.Valid.Domain.Common;

/// <summary>A cross-aggregate concept in the explicit common domain namespace.</summary>
/// <param name="Code">The currency code.</param>
public readonly record struct Currency(string Code);
