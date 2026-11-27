using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>Admin approval projection.</summary>
/// <param name="Status">The final decision state.</param>
/// <param name="ActorId">The deciding staff subject.</param>
/// <param name="Reason">The recorded reason.</param>
/// <param name="OccurredAt">The server UTC decision instant.</param>
public sealed record ApprovalDto(ApprovalStatus Status, string? ActorId, string? Reason, DateTimeOffset? OccurredAt);
