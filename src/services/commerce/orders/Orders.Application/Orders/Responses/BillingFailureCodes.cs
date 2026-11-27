namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>Stable internal failure codes returned by the billing adapter.</summary>
public static class BillingFailureCodes
{
    /// <summary>Required provider configuration is absent or malformed; checkout stays unavailable.</summary>
    public const string InvalidConfiguration = "invalid_configuration";

    /// <summary>The provider could not be reached or returned a transient failure; retry later.</summary>
    public const string ProviderUnavailable = "provider_unavailable";

    /// <summary>The provider rejected a request for a reason other than configuration or ambiguity.</summary>
    public const string ProviderRejected = "provider_rejected";

    /// <summary>A configured recurring price amount, currency, interval, mode or active state did not match.</summary>
    public const string PriceMismatch = "price_mismatch";

    /// <summary>The provider object does not match the stored order, session or subscription binding.</summary>
    public const string BindingMismatch = "binding_mismatch";

    /// <summary>The provider invoice amount does not match the immutable price snapshot.</summary>
    public const string AmountMismatch = "amount_mismatch";

    /// <summary>The provider invoice currency does not match the immutable price snapshot.</summary>
    public const string CurrencyMismatch = "currency_mismatch";

    /// <summary>No unique provider session can be verified; automatic session creation stays stopped.</summary>
    public const string CheckoutUnavailable = "checkout_unavailable";

    /// <summary>The provider reports an idempotency key conflict; never issue a fresh key.</summary>
    public const string IdempotencyConflict = "idempotency_conflict";
}
