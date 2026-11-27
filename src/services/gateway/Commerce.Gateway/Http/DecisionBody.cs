using System.Text.Json.Serialization;

namespace Teck.Cloud.Commerce.Gateway.Http;

/// <summary>The final staff decision body.</summary>
public sealed class DecisionBody
{
    /// <summary>Gets the decision string.</summary>
    [JsonRequired]
    public string? Decision { get; init; }

    /// <summary>Gets the staff reason.</summary>
    [JsonRequired]
    public string? Reason { get; init; }

    /// <summary>Gets the observed order revision.</summary>
    [JsonRequired]
    public long? ExpectedRevision { get; init; }
}
