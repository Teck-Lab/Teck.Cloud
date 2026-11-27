using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>Admin payment projection.</summary>
/// <param name="Status">The effective payment status.</param>
/// <param name="PaidThrough">The current paid-period end, or null when not currently paid.</param>
public sealed record PaymentDto(PaymentStatus Status, DateTimeOffset? PaidThrough);
