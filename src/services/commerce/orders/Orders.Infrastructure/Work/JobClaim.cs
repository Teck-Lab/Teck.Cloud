using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Work;

/// <summary>A claimed provider work item.</summary>
/// <param name="JobId">The reconciliation job id.</param>
/// <param name="TenantId">The explicit tenant id.</param>
/// <param name="OrderId">The order id.</param>
/// <param name="Kind">The work kind.</param>
/// <param name="Attempts">The attempts already started.</param>
/// <param name="ProviderEventReceiptId">The triggering provider event receipt id, when applicable.</param>
/// <param name="LeaseToken">The lease token claimed for the batch.</param>
public sealed record JobClaim(
    Guid JobId,
    string TenantId,
    Guid OrderId,
    ReconciliationKind Kind,
    int Attempts,
    Guid? ProviderEventReceiptId,
    Guid LeaseToken);
