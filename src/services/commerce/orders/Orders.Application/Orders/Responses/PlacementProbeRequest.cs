namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>One read-only placement verification request derived from durable order facts.</summary>
/// <param name="TenantId">The immutable authenticated tenant identifier.</param>
/// <param name="OrderId">The Orders-owned order identifier.</param>
/// <param name="SubscriptionId">The Orders-owned subscription identifier.</param>
/// <param name="PlacementId">The allowlisted placement identifier recorded as staff evidence.</param>
/// <param name="Tier">The order tier, shared or dedicated.</param>
/// <param name="AddOnCodes">The canonical selected add-on codes; replicaReadAccess requires a standby check.</param>
public sealed record PlacementProbeRequest(
    Guid TenantId,
    Guid OrderId,
    Guid SubscriptionId,
    string PlacementId,
    string Tier,
    IReadOnlyList<string> AddOnCodes);
