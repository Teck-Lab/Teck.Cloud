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
    BillingReconciliation Snapshot) : ICommand<ErrorOr<bool>>, ITenantMessage
{
    /// <summary>Gets the bounded outstanding receipt ids snapshotted before the provider call.</summary>
    public IReadOnlyList<Guid> ReceiptIds { get; init; } = [];

    /// <summary>Gets the durable polling interval applied to a verified unpaid re-fetch, in seconds.</summary>
    public int RetrySeconds { get; init; }
}
