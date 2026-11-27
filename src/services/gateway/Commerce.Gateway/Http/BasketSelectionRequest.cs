using System.Text.Json.Serialization;

namespace Teck.Cloud.Commerce.Gateway.Http;

/// <summary>The selected offer and optional add-ons in a basket request.</summary>
public sealed class BasketSelectionRequest
{
    /// <summary>Gets the opaque offer identifier.</summary>
    [JsonRequired]
    public string? OfferId { get; init; }

    /// <summary>Gets the requested add-on codes.</summary>
    [JsonRequired]
    public IReadOnlyList<string>? AddOnCodes { get; init; }
}
