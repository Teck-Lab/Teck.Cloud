namespace Teck.Cloud.Commerce.Gateway.Models;

/// <summary>Final staff approval facts.</summary>
/// <param name="Status">Approval state.</param>
/// <param name="ActorId">Recorded staff subject, or null while pending.</param>
/// <param name="Reason">Trimmed staff reason, or null while pending.</param>
/// <param name="OccurredAt">Decision instant, or null while pending.</param>
public sealed record ApprovalView(string Status, string? ActorId, string? Reason, string? OccurredAt);
