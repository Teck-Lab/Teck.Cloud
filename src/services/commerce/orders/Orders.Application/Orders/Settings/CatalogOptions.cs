namespace Teck.Cloud.Commerce.Orders.Application.Orders.Settings;

/// <summary>Server-owned catalog configuration. Browser input never supplies prices.</summary>
public sealed class CatalogOptions
{
    /// <summary>Gets or sets the uppercase ISO 4217 currency.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>Gets the configured offers.</summary>
    public IList<CatalogOfferOptions> Offers { get; } = [];
}
