using ErrorOr;

namespace Teck.Cloud.Commerce.Orders.Domain.Aggregates.Baskets;

/// <summary>A canonical, sorted and deduplicated basket selection.</summary>
/// <param name="OfferId">The selected catalog offer.</param>
/// <param name="AddOnCodes">The sorted add-on codes.</param>
public sealed record BasketSelection(string OfferId, IReadOnlyList<string> AddOnCodes)
{
    /// <summary>Canonicalizes a selection, rejecting duplicate or malformed codes.</summary>
    /// <param name="offerId">The selected offer id.</param>
    /// <param name="addOnCodes">The requested add-on codes in any order.</param>
    /// <returns>The canonical selection, or a stable validation error.</returns>
    public static ErrorOr<BasketSelection> Create(string offerId, IEnumerable<string>? addOnCodes)
    {
        if (string.IsNullOrWhiteSpace(offerId) || offerId.Length > 64)
        {
            return BasketErrors.InvalidOffer;
        }

        var requested = addOnCodes?.ToArray() ?? [];
        if (requested.Any(code => string.IsNullOrWhiteSpace(code) || code.Length > 64 || code.Contains(',', StringComparison.Ordinal)))
        {
            return BasketErrors.UnsupportedOption;
        }

        if (new HashSet<string>(requested, StringComparer.Ordinal).Count != requested.Length)
        {
            return BasketErrors.DuplicateAddOn;
        }

        var canonical = requested.Order(StringComparer.Ordinal).ToArray();
        return new BasketSelection(offerId, canonical);
    }

    /// <summary>Encodes the sorted codes for storage.</summary>
    /// <returns>The comma-separated canonical codes.</returns>
    public string EncodeAddOnCodes() => string.Join(',', AddOnCodes);

    /// <summary>Decodes stored add-on codes without interpreting them.</summary>
    /// <param name="value">The stored comma-separated value.</param>
    /// <returns>The decoded codes.</returns>
    public static IReadOnlyList<string> DecodeAddOnCodes(string? value) =>
        string.IsNullOrEmpty(value) ? [] : value.Split(',', StringSplitOptions.RemoveEmptyEntries);
}
