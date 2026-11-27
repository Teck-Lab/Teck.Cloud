namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>The Stripe account mode the server is configured for.</summary>
public enum BillingProviderMode
{
    /// <summary>Stripe test mode; no real money moves.</summary>
    Test = 0,

    /// <summary>Stripe live mode.</summary>
    Live = 1,
}
