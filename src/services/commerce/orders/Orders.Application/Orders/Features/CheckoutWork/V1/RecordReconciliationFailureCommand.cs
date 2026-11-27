using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.MultiTenancy;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.CheckoutWork.V1;

/// <summary>Records a sanitized provider work failure and reschedules or abandons the job.</summary>
/// <param name="TenantId">The validated tenant id.</param>
/// <param name="OrderId">The order id.</param>
/// <param name="JobId">The claimed job id.</param>
/// <param name="LeaseToken">The claimed lease token.</param>
/// <param name="Category">The sanitized failure category.</param>
/// <param name="NextDueAt">The next attempt instant.</param>
/// <param name="Abandon">Whether the work must stop automatically.</param>
public sealed record RecordReconciliationFailureCommand(
    string TenantId,
    Guid OrderId,
    Guid JobId,
    Guid LeaseToken,
    string Category,
    DateTimeOffset NextDueAt,
    bool Abandon) : ICommand<ErrorOr<bool>>, ITenantMessage;
