namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>The bound order facts needed for provider checkout creation, recovery and reconciliation.</summary>
/// <param name="TenantId">The immutable authenticated tenant identifier.</param>
/// <param name="OrderId">The Orders-owned order identifier, used to derive the stable provider idempotency key.</param>
/// <param name="SubscriptionId">The Orders-owned subscription identifier.</param>
/// <param name="CatalogKey">The selected catalog offer identifier.</param>
/// <param name="Tier">The selected database tier.</param>
/// <param name="Lines">The immutable provider-verified price snapshot.</param>
/// <param name="CheckoutSessionId">The stored provider checkout session binding, if one was persisted.</param>
/// <param name="ProviderSubscriptionId">The stored provider subscription binding, if one was persisted.</param>
/// <param name="RequestedAt">The immutable intent creation time used to bound ambiguous provider creation.</param>
public sealed record BillingOrder(
    Guid TenantId,
    Guid OrderId,
    Guid SubscriptionId,
    string CatalogKey,
    string Tier,
    IReadOnlyList<BillingVerifiedPriceLine> Lines,
    string? CheckoutSessionId,
    string? ProviderSubscriptionId,
    DateTimeOffset RequestedAt);
