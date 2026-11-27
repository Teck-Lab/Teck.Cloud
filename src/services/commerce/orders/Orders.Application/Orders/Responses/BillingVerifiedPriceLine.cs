namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>A provider-verified price line that becomes part of the immutable order snapshot.</summary>
/// <param name="Code">The catalog offer identifier or add-on code.</param>
/// <param name="ProviderPriceId">The opaque provider recurring price identifier.</param>
/// <param name="UnitAmountMinor">The provider-verified recurring amount in minor currency units.</param>
/// <param name="Currency">The provider-verified uppercase ISO 4217 currency.</param>
/// <param name="Interval">The provider-verified recurring interval.</param>
/// <param name="IntervalCount">The provider-verified billing cycle interval count.</param>
public sealed record BillingVerifiedPriceLine(
    string Code,
    string ProviderPriceId,
    long UnitAmountMinor,
    string Currency,
    string Interval,
    int IntervalCount);
