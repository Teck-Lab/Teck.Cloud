using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.MultiTenancy;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.ReconcileEntitlement.V1;

/// <summary>Recomputes the desired generation from current durable gates and time.</summary>
/// <param name="TenantId">The validated tenant id.</param>
/// <param name="OrderId">The order id.</param>
public sealed record ReconcileEntitlementCommand(string TenantId, Guid OrderId)
    : ICommand<ErrorOr<bool>>, ITenantMessage;
