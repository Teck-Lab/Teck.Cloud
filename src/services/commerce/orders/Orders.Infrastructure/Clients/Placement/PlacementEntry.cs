using PlacementView = Teck.Cloud.Commerce.Orders.Application.Orders.Responses.PlacementRegistration;

namespace Teck.Cloud.Commerce.Orders.Infrastructure.Clients.Placement;

/// <summary>One allowlisted placement registry entry including infrastructure-only secret references.</summary>
/// <param name="PlacementId">The opaque placement identifier.</param>
/// <param name="Tier">The placement tier, shared or dedicated.</param>
/// <param name="PrimarySecretReference">The configuration reference for the TLS primary connection string.</param>
/// <param name="ReplicaSecretReference">The optional configuration reference for the TLS read-only standby connection string.</param>
/// <param name="DedicatedTenantId">The only tenant a dedicated placement may serve, null for shared.</param>
internal sealed record PlacementEntry(
    string PlacementId,
    string Tier,
    string PrimarySecretReference,
    string? ReplicaSecretReference,
    Guid? DedicatedTenantId)
{
    /// <summary>Creates the safe application-facing registration view.</summary>
    /// <returns>The registration view without secret references.</returns>
    public PlacementView ToView() => new(
        PlacementId,
        Tier,
        ReplicaSecretReference is not null,
        DedicatedTenantId);
}
