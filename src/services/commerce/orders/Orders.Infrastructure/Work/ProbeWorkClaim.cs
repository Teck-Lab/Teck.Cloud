namespace Teck.Cloud.Commerce.Orders.Infrastructure.Work;

/// <summary>A claimed probe-work item with the order facts needed by the read-only probe.</summary>
/// <param name="WorkId">The probe-work row id.</param>
/// <param name="TenantId">The tenant id.</param>
/// <param name="OrderId">The order id.</param>
/// <param name="SubscriptionId">The subscription id.</param>
/// <param name="EvidenceRevision">The evidence revision.</param>
/// <param name="PlacementId">The placement id.</param>
/// <param name="Tier">The stored tier value.</param>
/// <param name="AddOnCodesCsv">The canonical add-on codes.</param>
public sealed record ProbeWorkClaim(
    Guid WorkId,
    string TenantId,
    Guid OrderId,
    Guid SubscriptionId,
    uint EvidenceRevision,
    string PlacementId,
    int Tier,
    string AddOnCodesCsv);
