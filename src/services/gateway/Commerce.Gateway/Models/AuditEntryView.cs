namespace Teck.Cloud.Commerce.Gateway.Models;

/// <summary>One recorded staff action.</summary>
/// <param name="OrderId">Audited order identifier.</param>
/// <param name="ActorId">Recorded staff subject.</param>
/// <param name="Action">Recorded action.</param>
/// <param name="Reason">Trimmed reason, or null.</param>
/// <param name="OccurredAt">Server UTC instant.</param>
public sealed record AuditEntryView(Guid OrderId, string ActorId, string Action, string? Reason, string OccurredAt);
