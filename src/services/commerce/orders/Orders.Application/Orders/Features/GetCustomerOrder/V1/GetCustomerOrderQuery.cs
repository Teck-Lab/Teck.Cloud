using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.GetCustomerOrder.V1;

/// <summary>Reads one order within the authenticated tenant and subject scope.</summary>
/// <param name="TenantId">The validated tenant id.</param>
/// <param name="SubjectId">The authenticated subject.</param>
/// <param name="OrderId">The requested order id.</param>
public sealed record GetCustomerOrderQuery(string TenantId, string SubjectId, Guid OrderId)
    : IQuery<ErrorOr<CustomerOrderDto>>, ITenantMessage;
