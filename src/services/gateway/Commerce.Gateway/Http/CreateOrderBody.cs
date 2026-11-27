using System.Text.Json.Serialization;

namespace Teck.Cloud.Commerce.Gateway.Http;

/// <summary>The create-order body.</summary>
public sealed class CreateOrderBody
{
    /// <summary>Gets the observed basket revision.</summary>
    [JsonRequired]
    public long? BasketRevision { get; init; }
}
