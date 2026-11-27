namespace Teck.Cloud.Commerce.Orders.Application.Orders.Responses;

/// <summary>A safe view of one allowlisted placement registry entry; secret references are never exposed.</summary>
/// <param name="PlacementId">The opaque placement identifier accepted from authorized staff evidence.</param>
/// <param name="Tier">The placement tier, shared or dedicated.</param>
/// <param name="ReplicaConfigured">Whether a read-only replica target is configured for the placement.</param>
/// <param name="DedicatedTenantId">The only tenant a dedicated placement may serve, null for shared.</param>
public sealed record PlacementRegistration(
    string PlacementId,
    string Tier,
    bool ReplicaConfigured,
    Guid? DedicatedTenantId);
