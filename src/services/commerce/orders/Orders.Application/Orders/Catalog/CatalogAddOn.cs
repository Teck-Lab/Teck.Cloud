namespace Teck.Cloud.Commerce.Orders.Application.Orders.Catalog;

/// <summary>An advertised add-on.</summary>
/// <param name="Code">The add-on code.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="AmountMinor">The monthly minor-unit price.</param>
public sealed record CatalogAddOn(string Code, string DisplayName, long AmountMinor);
