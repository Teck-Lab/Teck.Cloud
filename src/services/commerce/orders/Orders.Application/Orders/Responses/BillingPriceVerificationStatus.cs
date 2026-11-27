namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>Whether the configured recurring prices were verified against the provider.</summary>
public enum BillingPriceVerificationStatus
{
    /// <summary>Every configured line was verified; checkout may use the snapshot.</summary>
    Verified = 0,

    /// <summary>Configuration is absent or invalid, the provider rejected it, or the provider is unavailable.</summary>
    Unavailable = 1,
}
