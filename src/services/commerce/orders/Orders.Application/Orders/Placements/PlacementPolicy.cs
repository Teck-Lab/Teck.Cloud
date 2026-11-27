using ErrorOr;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;
using Teck.Cloud.Commerce.Orders.Domain.Aggregates.Orders;
using Teck.Cloud.Commerce.Orders.Domain.Common;

namespace Teck.Cloud.Commerce.Orders.Application.Orders.Placements;

/// <summary>Validates staff-selected placements against the trusted allowlist.</summary>
public static class PlacementPolicy
{
    /// <summary>Resolves the placement only when it matches the order tier and dedicated tenant scope.</summary>
    /// <param name="order">The target order.</param>
    /// <param name="placement">The configured allowlist.</param>
    /// <param name="placementId">The staff-selected placement id.</param>
    /// <returns>The registration, or a stable error.</returns>
    public static ErrorOr<PlacementRegistration> Resolve(Order order, IPlacementProbe placement, string placementId)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(placement);
        var registration = placement.FindRegistration(placementId);
        if (registration is null)
        {
            return OrderErrors.UnknownPlacement;
        }

        var expectedTier = order.Tier == Tier.Dedicated ? "dedicated" : "shared";
        var tierMatches = string.Equals(registration.Tier, expectedTier, StringComparison.OrdinalIgnoreCase);
        var tenantMatches = order.Tier != Tier.Dedicated
            || (Guid.TryParse(order.TenantId, out var tenant) && registration.DedicatedTenantId == tenant);
        return tierMatches && tenantMatches ? registration : OrderErrors.UnknownPlacement;
    }
}
