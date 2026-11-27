namespace Teck.Cloud.Commerce.Gateway.Models;

/// <summary>A server-priced database offer.</summary>
/// <param name="OfferId">Opaque server-issued offer identifier.</param>
/// <param name="Tier">Effective database tier.</param>
/// <param name="DisplayName">Client display label.</param>
/// <param name="BasePrice">Server-owned recurring base price.</param>
/// <param name="AddOns">Currently offered optional add-ons.</param>
public sealed record OfferView(string OfferId, string Tier, string DisplayName, MoneyView BasePrice, IReadOnlyList<AddOnView> AddOns);
