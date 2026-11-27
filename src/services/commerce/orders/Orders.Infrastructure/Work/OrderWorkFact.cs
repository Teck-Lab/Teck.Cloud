namespace Teck.Cloud.Commerce.Orders.Infrastructure.Work;

/// <summary>Read-only order facts needed by provider workers.</summary>
/// <param name="OrderId">The order id.</param>
/// <param name="TenantId">The tenant id.</param>
/// <param name="SubscriptionId">The subscription id.</param>
/// <param name="OfferId">The catalog offer id.</param>
/// <param name="Tier">The stored tier value.</param>
/// <param name="AddOnCodesCsv">The canonical add-on codes.</param>
/// <param name="CatalogPriceSnapshot">The immutable catalog price request snapshot.</param>
/// <param name="VerifiedPriceSnapshot">The verified provider price snapshot.</param>
/// <param name="SessionId">The bound provider session id.</param>
/// <param name="ProviderSubscriptionId">The verified provider subscription id.</param>
/// <param name="CheckoutRequestedAt">The immutable first checkout instant.</param>
/// <param name="CheckoutCreateAttempts">The provider creation attempts started.</param>
/// <param name="PaymentStatus">The stored payment status value.</param>
/// <param name="PaidThrough">The stored paid-period end.</param>
/// <param name="ApprovalStatus">The stored approval status value.</param>
/// <param name="DesiredEnabled">Whether a desired generation enables entitlement.</param>
/// <param name="DesiredNotAfter">The current desired validity end.</param>
/// <param name="DesiredRevision">The current desired generation.</param>
public sealed record OrderWorkFact(
    Guid OrderId,
    string TenantId,
    Guid SubscriptionId,
    string OfferId,
    int Tier,
    string AddOnCodesCsv,
    string CatalogPriceSnapshot,
    string? VerifiedPriceSnapshot,
    string? SessionId,
    string? ProviderSubscriptionId,
    DateTimeOffset CheckoutRequestedAt,
    int CheckoutCreateAttempts,
    int PaymentStatus,
    DateTimeOffset? PaidThrough,
    int ApprovalStatus,
    bool DesiredEnabled,
    DateTimeOffset? DesiredNotAfter,
    uint DesiredRevision);
