namespace Commerce.IntegrationTests.Infrastructure;

/// <summary>Authoritative manual-operator binding for one placement.</summary>
/// <param name="TenantId">The immutable tenant.</param>
/// <param name="OrderId">The order.</param>
/// <param name="SubscriptionId">The subscription.</param>
/// <param name="PlacementId">The allowlisted placement.</param>
public sealed record PlacementBinding(Guid TenantId, Guid OrderId, Guid SubscriptionId, Guid PlacementId);
