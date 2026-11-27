using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.MultiTenancy;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.CheckoutWork.V1;

/// <summary>Completes an already-satisfied provider work item under its live lease.</summary>
/// <param name="TenantId">The validated tenant id.</param>
/// <param name="OrderId">The order id.</param>
/// <param name="JobId">The claimed job id.</param>
/// <param name="LeaseToken">The claimed lease token.</param>
public sealed record CompleteReconciliationJobCommand(
    string TenantId,
    Guid OrderId,
    Guid JobId,
    Guid LeaseToken) : ICommand<ErrorOr<bool>>, ITenantMessage;
