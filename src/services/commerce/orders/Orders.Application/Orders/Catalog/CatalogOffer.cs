using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Catalog;

/// <summary>An advertised offer.</summary>
/// <param name="OfferId">The opaque offer id.</param>
/// <param name="Tier">The tier.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="AmountMinor">The monthly base minor-unit price.</param>
/// <param name="Currency">The uppercase ISO 4217 currency.</param>
/// <param name="AddOns">The advertised add-ons.</param>
public sealed record CatalogOffer(string OfferId, Tier Tier, string DisplayName, long AmountMinor, string Currency, IReadOnlyList<CatalogAddOn> AddOns);
