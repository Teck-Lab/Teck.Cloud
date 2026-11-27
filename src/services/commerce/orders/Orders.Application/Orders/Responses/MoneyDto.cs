namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>Wire money in minor units.</summary>
/// <param name="AmountMinor">The positive minor-unit amount.</param>
/// <param name="Currency">The uppercase ISO 4217 currency.</param>
public sealed record MoneyDto(long AmountMinor, string Currency);
