using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.GetAdminOrder.V1;

/// <summary>Reads one order for an authorized selected tenant.</summary>
/// <param name="TenantId">The selected tenant id.</param>
/// <param name="OrderId">The requested order id.</param>
public sealed record GetAdminOrderQuery(string TenantId, Guid OrderId)
    : IQuery<ErrorOr<AdminOrderDto>>, ITenantMessage;
