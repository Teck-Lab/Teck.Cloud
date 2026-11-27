using System.Text.Json.Serialization;

namespace Teck.Cloud.Commerce.Gateway.Http;

/// <summary>The PUT basket body.</summary>
public sealed class PutBasketBody
{
    /// <summary>Gets the requested selection, or null to clear.</summary>
    [JsonRequired]
    public BasketSelectionRequest? Selection { get; init; }

    /// <summary>Gets the last observed basket revision.</summary>
    [JsonRequired]
    public long? ExpectedRevision { get; init; }
}
