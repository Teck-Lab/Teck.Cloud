namespace Teck.Cloud.Commerce.Orders.Application.Orders.Settings;

/// <summary>One configured catalog offer.</summary>
public sealed class CatalogOfferOptions
{
    /// <summary>Gets or sets the opaque offer id.</summary>
    public string OfferId { get; set; } = string.Empty;

    /// <summary>Gets or sets the offering tier name.</summary>
    public string Tier { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets or sets the base monthly price in minor units.</summary>
    public long AmountMinor { get; set; }

    /// <summary>Gets the optional add-ons.</summary>
    public IList<CatalogAddOnOptions> AddOns { get; } = [];
}
