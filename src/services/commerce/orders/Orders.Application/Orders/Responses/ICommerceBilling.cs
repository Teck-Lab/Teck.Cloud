namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>Stripe Billing is the authority for verified payment facts.</summary>
public interface ICommerceBilling
{
    /// <summary>Resolves configured recurring prices and verifies amounts, intervals and currency against the provider.</summary>
    /// <param name="price">The server catalog price selection to verify.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The verified provider price snapshot, or a stable unavailable result.</returns>
    Task<BillingPriceVerification> ValidatePriceAsync(BillingPrice price, CancellationToken cancellationToken);

    /// <summary>Creates a hosted subscription checkout session with a stable provider idempotency key.</summary>
    /// <param name="order">The bound order facts and immutable price snapshot.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>A created checkout, a retryable pending outcome or a checkout-unavailable outcome.</returns>
    Task<BillingCheckout> CreateAsync(BillingOrder order, CancellationToken cancellationToken);

    /// <summary>Recovers an already-created provider session without issuing a new idempotency key or subscription.</summary>
    /// <param name="order">The bound order facts and immutable price snapshot.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>A recovered checkout, a retryable pending outcome or a checkout-unavailable outcome.</returns>
    Task<BillingCheckout> RecoverAsync(BillingOrder order, CancellationToken cancellationToken);

    /// <summary>Re-fetches the provider session, subscription and current invoice and verifies the order binding.</summary>
    /// <param name="order">The bound order facts and immutable price snapshot.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The authoritative provider snapshot; notifications are never used as proof.</returns>
    Task<BillingReconciliation> ReconcileAsync(BillingOrder order, CancellationToken cancellationToken);

    /// <summary>Verifies the exact raw body signature, timestamp, account and mode of a provider webhook.</summary>
    /// <param name="rawBody">The exact UTF-8 webhook request body.</param>
    /// <param name="signatureHeader">The provider signature header value.</param>
    /// <returns>A trigger notice, or <see langword="null"/> when the signature, account or mode is invalid.</returns>
    BillingNotice? ParseWebhook(string rawBody, string signatureHeader);
}
