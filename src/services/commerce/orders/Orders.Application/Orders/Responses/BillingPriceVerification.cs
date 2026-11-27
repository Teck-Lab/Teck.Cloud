namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>The result of resolving and verifying configured recurring prices against the provider.</summary>
/// <param name="Status">Whether checkout may use the verified price snapshot.</param>
/// <param name="Mode">The configured provider mode.</param>
/// <param name="AccountId">The configured provider account identifier when available.</param>
/// <param name="Lines">The verified provider price lines, empty unless <paramref name="Status"/> is <see cref="BillingPriceVerificationStatus.Verified"/>.</param>
/// <param name="FailureCode">A stable lowercase failure code, null on success.</param>
public sealed record BillingPriceVerification(
    BillingPriceVerificationStatus Status,
    BillingProviderMode Mode,
    string? AccountId,
    IReadOnlyList<BillingVerifiedPriceLine> Lines,
    string? FailureCode);
