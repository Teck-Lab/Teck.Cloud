using System.Text.Json.Serialization;

namespace Teck.Cloud.Commerce.Gateway.Http;

/// <summary>The placement-evidence body.</summary>
public sealed class PlacementEvidenceBody
{
    /// <summary>Gets the allowlisted placement identifier.</summary>
    [JsonRequired]
    public string? PlacementId { get; init; }

    /// <summary>Gets the nonsecret audit reference.</summary>
    [JsonRequired]
    public string? EvidenceReference { get; init; }

    /// <summary>Gets the observed order revision.</summary>
    [JsonRequired]
    public long? ExpectedRevision { get; init; }
}
