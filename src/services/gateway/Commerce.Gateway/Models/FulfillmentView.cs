namespace Teck.Cloud.Commerce.Gateway.Models;

/// <summary>Recorded placement evidence and asynchronous verification facts.</summary>
/// <param name="Status">Verification state.</param>
/// <param name="PlacementId">Allowlisted placement identifier, or null.</param>
/// <param name="EvidenceReference">Nonsecret audit reference, or null.</param>
/// <param name="ObservedAt">Successful probe instant, or null.</param>
/// <param name="FailureCode">Retryable or terminal failure classification, or null.</param>
public sealed record FulfillmentView(string Status, Guid? PlacementId, string? EvidenceReference, string? ObservedAt, string? FailureCode);
