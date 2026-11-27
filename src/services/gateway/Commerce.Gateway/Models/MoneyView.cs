namespace Teck.Cloud.Commerce.Gateway.Models;

/// <summary>A positive amount in minor units with an uppercase ISO 4217 currency.</summary>
/// <param name="AmountMinor">Server-owned price in minor units.</param>
/// <param name="Currency">Uppercase ISO 4217 currency code.</param>
public sealed record MoneyView(long AmountMinor, string Currency);
