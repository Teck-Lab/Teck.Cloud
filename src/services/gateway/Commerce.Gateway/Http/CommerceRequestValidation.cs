using System.Text.RegularExpressions;

namespace Teck.Cloud.Commerce.Gateway.Http;

/// <summary>Strict parsing for the small allowlisted commerce surface.</summary>
public static partial class CommerceRequestValidation
{
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]{0,63}$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex AddOnCode { get; }

    /// <summary>Validates that a list request carries no undeclared query inputs.</summary>
    /// <param name="query">The request query collection.</param>
    /// <param name="allowed">The exactly allowed names.</param>
    /// <returns>True when every supplied key is allowed.</returns>
    public static bool HasOnly(Microsoft.AspNetCore.Http.IQueryCollection query, params string[] allowed)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(allowed);
        return query.Keys.All(key => allowed.Contains(key, StringComparer.Ordinal));
    }

    /// <summary>Parses the bounded page limit, defaulting to 25.</summary>
    /// <param name="value">The raw single query value.</param>
    /// <param name="limit">The parsed limit.</param>
    /// <returns>True when absent or a valid integer in 1..100.</returns>
    public static bool TryLimit(Microsoft.Extensions.Primitives.StringValues value, out uint limit)
    {
        limit = 25;
        if (value.Count == 0)
        {
            return true;
        }

        if (value.Count != 1
            || !uint.TryParse(value[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            || parsed is < 1 or > 100)
        {
            return false;
        }

        limit = parsed;
        return true;
    }

    /// <summary>Parses an optional opaque cursor without decoding or rewriting it.</summary>
    /// <param name="value">The raw single query value.</param>
    /// <param name="cursor">The original cursor, or null when absent.</param>
    /// <returns>True when absent or exactly one bounded nonempty string.</returns>
    public static bool TryCursor(Microsoft.Extensions.Primitives.StringValues value, out string? cursor)
    {
        cursor = null;
        if (value.Count == 0)
        {
            return true;
        }

        cursor = value[0];
        return value.Count == 1 && !string.IsNullOrEmpty(cursor) && cursor.Length <= 512;
    }

    /// <summary>Validates a canonical lowercase UUID route value.</summary>
    /// <param name="value">The raw value.</param>
    /// <param name="id">The parsed identifier.</param>
    /// <returns>True only for the exact published pattern.</returns>
    public static bool TryUuid(string? value, out Guid id) =>
        Guid.TryParseExact(value, "D", out id) && string.Equals(value, id.ToString("D"), StringComparison.Ordinal);

    /// <summary>Converts a bounded revision without overflow.</summary>
    /// <param name="value">The nullable wire value.</param>
    /// <param name="minimum">The inclusive contract minimum.</param>
    /// <param name="revision">The parsed revision.</param>
    /// <returns>True when the value is present and within the uint32 range.</returns>
    public static bool TryRevision(long? value, uint minimum, out uint revision)
    {
        revision = 0;
        if (value is null or < 0 or > uint.MaxValue)
        {
            return false;
        }

        revision = (uint)value.Value;
        return revision >= minimum;
    }

    /// <summary>Validates one optional add-on code.</summary>
    /// <param name="code">The candidate code.</param>
    /// <returns>True for a bounded published code.</returns>
    public static bool IsAddOnCode(string? code) => code is not null && AddOnCode.IsMatch(code);

    /// <summary>Validates a basket selection and rejects duplicate add-ons before delegation.</summary>
    /// <param name="selection">The request selection or null to clear.</param>
    /// <returns>True when the selection is representable and duplicate-free.</returns>
    public static bool IsSelectionValid(BasketSelectionRequest? selection)
    {
        if (selection is null)
        {
            return true;
        }

        if (string.IsNullOrEmpty(selection.OfferId) || selection.OfferId.Length > 64 || selection.AddOnCodes is null || selection.AddOnCodes.Count > 20)
        {
            return false;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        return selection.AddOnCodes.All(code => IsAddOnCode(code) && seen.Add(code));
    }
}
