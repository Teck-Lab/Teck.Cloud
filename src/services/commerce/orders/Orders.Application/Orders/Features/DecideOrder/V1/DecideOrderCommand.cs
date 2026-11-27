using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;
using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.DecideOrder.V1;

/// <summary>Records the first and final staff decision for one selected-tenant order.</summary>
/// <param name="TenantId">The selected tenant id.</param>
/// <param name="OrderId">The order id.</param>
/// <param name="Decision">The decision.</param>
/// <param name="Reason">The trimmed nonblank reason.</param>
/// <param name="ExpectedRevision">The expected order revision.</param>
/// <param name="ActorId">The authenticated staff subject.</param>
public sealed record DecideOrderCommand(
    string TenantId,
    Guid OrderId,
    Decision Decision,
    string Reason,
    uint ExpectedRevision,
    string ActorId) : ICommand<ErrorOr<AdminOrderDto>>, ITenantMessage;
