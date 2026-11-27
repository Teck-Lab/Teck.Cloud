namespace Teck.Cloud.Commerce.Orders.Application.Orders.Cursor;

/// <summary>The exact authorization scope a cursor is bound to.</summary>
/// <param name="Surface">The list surface.</param>
/// <param name="TenantId">The validated tenant id.</param>
/// <param name="SubjectId">The authenticated customer subject, or null for staff.</param>
public sealed record OrderCursorScope(OrderListSurface Surface, string TenantId, string? SubjectId);
