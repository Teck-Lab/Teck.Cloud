namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>The outcome of a checkout create or recovery attempt.</summary>
public enum BillingCheckoutStatus
{
    /// <summary>A usable, binding-verified hosted checkout URL exists.</summary>
    Created = 0,

    /// <summary>The provider attempt is ambiguous or temporarily unavailable; retry with the same idempotency key.</summary>
    Pending = 1,

    /// <summary>Checkout must stay unavailable; do not create another provider subscription or idempotency key.</summary>
    Unavailable = 2,
}
