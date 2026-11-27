namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>A server-catalog price line that must be verified against the provider before checkout.</summary>
/// <param name="Code">The catalog offer identifier or add-on code.</param>
/// <param name="UnitAmountMinor">The expected recurring amount in minor currency units.</param>
/// <param name="Currency">The expected uppercase ISO 4217 currency.</param>
/// <param name="Interval">The expected recurring interval; only month is supported.</param>
/// <param name="IntervalCount">The expected number of intervals per billing cycle; only 1 is supported.</param>
public sealed record BillingPriceLine(
    string Code,
    long UnitAmountMinor,
    string Currency,
    string Interval,
    int IntervalCount);
