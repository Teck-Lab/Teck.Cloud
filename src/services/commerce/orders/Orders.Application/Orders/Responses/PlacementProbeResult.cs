namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>A placement probe outcome with its stable classification and observation time.</summary>
/// <param name="Status">The classified probe outcome.</param>
/// <param name="PlacementId">The placement identifier the outcome belongs to.</param>
/// <param name="ObservedAt">The server UTC observation time.</param>
/// <param name="FailureCode">A stable lowercase failure code, null when verified.</param>
public sealed record PlacementProbeResult(
    PlacementProbeStatus Status,
    string PlacementId,
    DateTimeOffset ObservedAt,
    string? FailureCode);
