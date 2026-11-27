using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.CheckCurrentEntitlement.V1;

/// <summary>Answers the scoped Licensing workload from durable Orders facts only.</summary>
/// <param name="TenantId">The validated tenant id.</param>
/// <param name="OrderId">The bound order id.</param>
/// <param name="SubscriptionId">The bound subscription id.</param>
/// <param name="DesiredRevision">The desired generation being verified.</param>
public sealed record CheckCurrentEntitlementQuery(
    string TenantId,
    Guid OrderId,
    Guid SubscriptionId,
    uint DesiredRevision) : IQuery<ErrorOr<EntitlementCheckDto>>, ITenantMessage;
