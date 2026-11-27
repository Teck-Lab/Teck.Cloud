namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>Wire basket.</summary>
/// <param name="Selection">The selection, or null when empty.</param>
/// <param name="Revision">The current revision.</param>
/// <param name="Total">The snapshot total, or null when empty.</param>
public sealed record BasketDto(BasketSelectionDto? Selection, uint Revision, MoneyDto? Total);
