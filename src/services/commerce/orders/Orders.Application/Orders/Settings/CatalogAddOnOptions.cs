namespace Teck.Cloud.Commerce.Orders.Application.Orders.Settings;

/// <summary>One optional priced add-on.</summary>
public sealed class CatalogAddOnOptions
{
    /// <summary>Gets or sets the add-on code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets or sets the monthly price in minor units.</summary>
    public long AmountMinor { get; set; }
}
