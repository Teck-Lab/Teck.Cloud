using System.Globalization;

namespace Teck.Cloud.Commerce.Gateway.Models;

/// <summary>Formats published instants as RFC 3339 UTC with a Z suffix.</summary>
public static class CommerceTimes
{
    /// <summary>Formats one UTC instant without a numeric offset.</summary>
    /// <param name="value">The instant.</param>
    /// <returns>The wire representation.</returns>
    public static string Format(DateTimeOffset value)
    {
        var text = value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture);
        return text.Replace(".Z", "Z", StringComparison.Ordinal);
    }
}
