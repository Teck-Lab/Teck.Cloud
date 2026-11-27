using System.Text.Json.Serialization;

namespace Teck.Cloud.Commerce.Gateway.Models;

/// <summary>The sanitized problem shape shared by the commerce gateways.</summary>
public sealed record CommerceProblemView
{
    /// <summary>Gets the stable problem type URI.</summary>
    public required string Type { get; init; }

    /// <summary>Gets the stable title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the HTTP status.</summary>
    public required int Status { get; init; }

    /// <summary>Gets the frozen stable code.</summary>
    public required string Code { get; init; }

    /// <summary>Gets the sanitized detail when one is published.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; init; }

    /// <summary>Gets the request instance when one is published.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Instance { get; init; }
}
