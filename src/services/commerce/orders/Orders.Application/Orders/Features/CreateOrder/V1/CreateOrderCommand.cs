using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.CreateOrder.V1;

/// <summary>Creates or replays the durable checkout intent for one basket revision.</summary>
/// <param name="TenantId">The validated tenant id.</param>
/// <param name="SubjectId">The authenticated subject.</param>
/// <param name="BasketRevision">The current basket revision.</param>
/// <param name="IdempotencyKey">The UUID idempotency key.</param>
public sealed record CreateOrderCommand(
    string TenantId,
    string SubjectId,
    uint BasketRevision,
    string IdempotencyKey) : ICommand<ErrorOr<CheckoutResultDto>>, ITenantMessage;
