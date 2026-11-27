namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>Wire basket selection.</summary>
/// <param name="OfferId">The selected offer id.</param>
/// <param name="AddOnCodes">The canonical add-on codes.</param>
public sealed record BasketSelectionDto(string OfferId, IReadOnlyList<string> AddOnCodes);
