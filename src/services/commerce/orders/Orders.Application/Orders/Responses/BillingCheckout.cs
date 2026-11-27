namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>A binding-verified hosted checkout session or the reason no URL is available.</summary>
/// <param name="Status">Whether a usable checkout URL exists.</param>
/// <param name="ProviderSessionId">The provider checkout session identifier when known.</param>
/// <param name="CheckoutUrl">The absolute HTTPS hosted checkout URL, only when <paramref name="Status"/> is <see cref="BillingCheckoutStatus.Created"/>.</param>
/// <param name="Mode">The configured provider mode.</param>
/// <param name="AccountId">The configured provider account identifier when available.</param>
/// <param name="ExpiresAt">The provider session expiry when known.</param>
/// <param name="FailureCode">A stable lowercase failure code, null on success.</param>
public sealed record BillingCheckout(
    BillingCheckoutStatus Status,
    string? ProviderSessionId,
    string? CheckoutUrl,
    BillingProviderMode Mode,
    string? AccountId,
    DateTimeOffset? ExpiresAt,
    string? FailureCode);
