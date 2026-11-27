namespace Teck.Cloud.Commerce.Licensing.Application.Entitlements.Ports;

/// <summary>Internal scoped call to Orders' authoritative current-entitlement check.</summary>
public interface IOrdersEntitlementClient
{
    /// <summary>Checks the exact binding and desired generation against current Orders facts.</summary>
    /// <param name="tenantId">The immutable organization UUID.</param>
    /// <param name="orderId">The bound order.</param>
    /// <param name="subscriptionId">The bound subscription.</param>
    /// <param name="desiredRevision">The exact desired entitlement generation.</param>
    /// <param name="cancellationToken">Call cancellation and deadline.</param>
    /// <returns>Eligible, not eligible, or an unavailable dependency.</returns>
    Task<EntitlementCheckStatus> CheckAsync(
        string tenantId,
        Guid orderId,
        Guid subscriptionId,
        uint desiredRevision,
        CancellationToken cancellationToken);
}
