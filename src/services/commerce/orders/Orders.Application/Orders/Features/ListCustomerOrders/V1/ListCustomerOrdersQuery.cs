using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.ListCustomerOrders.V1;

/// <summary>Lists the authenticated subject's orders with an opaque scoped cursor.</summary>
/// <param name="TenantId">The validated tenant id.</param>
/// <param name="SubjectId">The authenticated subject.</param>
/// <param name="Limit">The requested page size; zero means the default.</param>
/// <param name="Cursor">The optional opaque cursor.</param>
public sealed record ListCustomerOrdersQuery(
    string TenantId,
    string SubjectId,
    uint Limit,
    string? Cursor) : IQuery<ErrorOr<PagedResultDto<CustomerOrderDto>>>, ITenantMessage;
