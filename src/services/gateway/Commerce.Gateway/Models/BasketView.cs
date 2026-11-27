namespace Teck.Cloud.Commerce.Gateway.Models;

/// <summary>The caller's persisted basket projection.</summary>
/// <param name="Selection">Current selection, or null when cleared.</param>
/// <param name="Revision">Persisted basket revision.</param>
/// <param name="Total">Server-computed total, or null when empty.</param>
public sealed record BasketView(BasketSelectionView? Selection, uint Revision, MoneyView? Total);
