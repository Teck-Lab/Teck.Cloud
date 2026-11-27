using ErrorOr;
using SharedKernel.Core.CQRS;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Features.ApplyPaymentSnapshot.V1;

/// <summary>Applies an authoritative provider snapshot under the job lease and revision fence.</summary>
/// <param name="TenantId">The validated tenant id.</param>
/// <param name="OrderId">The order id.</param>
/// <param name="JobId">The claimed job id.</param>
/// <param name="LeaseToken">The claimed lease token.</param>
/// <param name="Snapshot">The authoritative provider re-fetch result.</param>
public sealed record ApplyPaymentSnapshotCommand(
    string TenantId,
    Guid OrderId,
    Guid JobId,
    Guid LeaseToken,
    BillingReconciliation Snapshot) : ICommand<ErrorOr<bool>>, ITenantMessage;
