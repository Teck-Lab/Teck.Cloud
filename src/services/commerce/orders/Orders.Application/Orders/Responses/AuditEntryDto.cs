using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>Admin audit entry.</summary>
/// <param name="OrderId">The audited order id.</param>
/// <param name="ActorId">The staff subject.</param>
/// <param name="Action">The recorded action.</param>
/// <param name="Reason">The recorded reason or evidence reference.</param>
/// <param name="OccurredAt">The server UTC instant.</param>
public sealed record AuditEntryDto(Guid OrderId, string ActorId, AuditAction Action, string? Reason, DateTimeOffset OccurredAt);
