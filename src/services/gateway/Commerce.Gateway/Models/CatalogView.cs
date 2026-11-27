namespace Teck.Cloud.Commerce.Gateway.Models;

/// <summary>The catalog visible to the authenticated customer.</summary>
/// <param name="Offers">Offered tiers.</param>
public sealed record CatalogView(IReadOnlyList<OfferView> Offers);
