namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>The immutable server catalog selection whose configured prices must match the provider.</summary>
/// <param name="CatalogKey">The selected catalog offer identifier.</param>
/// <param name="Tier">The database tier selected by the catalog offer.</param>
/// <param name="Lines">The base price followed by canonical add-on prices.</param>
public sealed record BillingPrice(
    string CatalogKey,
    string Tier,
    IReadOnlyList<BillingPriceLine> Lines);
