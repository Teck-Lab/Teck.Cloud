namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>The authoritative provider snapshot for one order, applied only from a server-side re-fetch.</summary>
/// <param name="Status">The authoritative payment state.</param>
/// <param name="Mode">The configured provider mode.</param>
/// <param name="AccountId">The configured provider account identifier when available.</param>
/// <param name="ProviderSessionId">The verified provider checkout session identifier, if bound.</param>
/// <param name="ProviderSubscriptionId">The verified provider subscription identifier, if bound.</param>
/// <param name="InvoiceId">The current invoice identifier, if one exists.</param>
/// <param name="ChargeId">The current invoice charge identifier, if one exists.</param>
/// <param name="PaymentIntentId">The current invoice payment intent identifier, if one exists.</param>
/// <param name="PaidPeriodStart">The latest provider invoice period start in UTC, if one exists.</param>
/// <param name="PaidPeriodEnd">The latest provider invoice period end in UTC, if one exists.</param>
/// <param name="AmountPaidMinor">The provider invoice paid amount in minor currency units, if one exists.</param>
/// <param name="Currency">The provider invoice uppercase ISO 4217 currency, if one exists.</param>
/// <param name="VerifiedAtUtc">The server UTC time at which the provider was consulted.</param>
/// <param name="FailureCode">A stable lowercase failure code, null when the provider state is authoritative.</param>
public sealed record BillingReconciliation(
    BillingEvidenceStatus Status,
    BillingProviderMode Mode,
    string? AccountId,
    string? ProviderSessionId,
    string? ProviderSubscriptionId,
    string? InvoiceId,
    string? ChargeId,
    string? PaymentIntentId,
    DateTimeOffset? PaidPeriodStart,
    DateTimeOffset? PaidPeriodEnd,
    long? AmountPaidMinor,
    string? Currency,
    DateTimeOffset VerifiedAtUtc,
    string? FailureCode);
