namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>A signature-verified provider notification; it is a trigger, never payment proof.</summary>
/// <param name="EventId">The provider event identifier used for the unique account/event receipt.</param>
/// <param name="EventType">The provider event type.</param>
/// <param name="ProviderCreatedAt">The provider event creation time in UTC.</param>
/// <param name="Mode">The verified provider mode.</param>
/// <param name="AccountId">The configured provider account identifier that the signature belongs to.</param>
/// <param name="OrderId">The bound order identifier when the verified payload carries it.</param>
/// <param name="CheckoutSessionId">The checkout session identifier when the payload carries it.</param>
/// <param name="ProviderSubscriptionId">The provider subscription identifier when the payload carries it.</param>
/// <param name="InvoiceId">The invoice identifier when the payload carries it.</param>
/// <param name="ChargeId">The charge identifier when the payload carries it.</param>
/// <param name="PaymentIntentId">The payment intent identifier when the payload carries it.</param>
/// <param name="DisputeId">The dispute identifier when the payload carries it.</param>
public sealed record BillingNotice(
    string EventId,
    string EventType,
    DateTimeOffset ProviderCreatedAt,
    BillingProviderMode Mode,
    string? AccountId,
    Guid? OrderId,
    string? CheckoutSessionId,
    string? ProviderSubscriptionId,
    string? InvoiceId,
    string? ChargeId,
    string? PaymentIntentId,
    string? DisputeId);
