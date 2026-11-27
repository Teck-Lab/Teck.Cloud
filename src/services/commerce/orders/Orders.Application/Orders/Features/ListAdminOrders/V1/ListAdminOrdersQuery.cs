using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.ListAdminOrders.V1;

/// <summary>Lists orders for one authorized selected tenant.</summary>
/// <param name="TenantId">The selected tenant id.</param>
/// <param name="Limit">The requested page size; zero means the default.</param>
/// <param name="Cursor">The optional opaque cursor.</param>
public sealed record ListAdminOrdersQuery(string TenantId, uint Limit, string? Cursor)
    : IQuery<ErrorOr<PagedResultDto<AdminOrderDto>>>, ITenantMessage;
