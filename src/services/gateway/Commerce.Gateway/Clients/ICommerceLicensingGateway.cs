using Teck.Cloud.Commerce.Gateway.Models;

namespace Teck.Cloud.Commerce.Gateway.Clients;

/// <summary>The internal Licensing read port used only by the customer gateway.</summary>
public interface ICommerceLicensingGateway
{
    /// <summary>Reads the current active grant for the exact binding and desired generation.</summary>
    /// <param name="tenantId">The authenticated customer tenant UUID.</param>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="subscriptionId">The subscription identifier.</param>
    /// <param name="desiredRevision">The internal desired entitlement generation from Orders.</param>
    /// <param name="cancellationToken">Call cancellation.</param>
    /// <returns>The signed license envelope.</returns>
    Task<LicenseView> GetCurrentLicenseAsync(string tenantId, Guid orderId, Guid subscriptionId, uint desiredRevision, CancellationToken cancellationToken);
}
