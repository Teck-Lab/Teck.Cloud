namespace Teck.Cloud.Commerce.Gateway.Models;

/// <summary>A requested offer and add-on combination.</summary>
/// <param name="OfferId">Opaque offer identifier.</param>
/// <param name="AddOnCodes">Canonical add-on codes.</param>
public sealed record BasketSelectionView(string OfferId, IReadOnlyList<string> AddOnCodes);
