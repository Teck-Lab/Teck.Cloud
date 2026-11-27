using Teck.Cloud.Commerce.Gateway.Models;

namespace Teck.Cloud.Commerce.Gateway.Clients;

/// <summary>The Orders read/write port used by the commerce gateways.</summary>
public interface ICommerceOrdersGateway
{
    /// <summary>Reads the offer catalog.</summary>
    /// <param name="cancellationToken">Call cancellation.</param>
    /// <returns>The catalog projection.</returns>
    Task<CatalogView> GetCatalogAsync(CancellationToken cancellationToken);

    /// <summary>Reads the authenticated customer's basket.</summary>
    /// <param name="cancellationToken">Call cancellation.</param>
    /// <returns>The basket projection.</returns>
    Task<BasketView> GetBasketAsync(CancellationToken cancellationToken);

    /// <summary>Replaces or clears the authenticated customer's basket.</summary>
    /// <param name="selection">The requested selection, or null to clear.</param>
    /// <param name="expectedRevision">The last observed basket revision.</param>
    /// <param name="cancellationToken">Call cancellation.</param>
    /// <returns>The persisted basket projection.</returns>
    Task<BasketView> PutBasketAsync(BasketSelectionView? selection, uint expectedRevision, CancellationToken cancellationToken);

    /// <summary>Creates or replays one idempotent checkout intent.</summary>
    /// <param name="basketRevision">The observed basket revision.</param>
    /// <param name="idempotencyKey">The validated client idempotency UUID.</param>
    /// <param name="cancellationToken">Call cancellation.</param>
    /// <returns>The bound order and current checkout availability.</returns>
    Task<CreateOrderSnapshot> CreateOrderAsync(uint basketRevision, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Reads one scoped customer order page.</summary>
    /// <param name="limit">Positive page limit.</param>
    /// <param name="cursor">Optional opaque cursor.</param>
    /// <param name="cancellationToken">Call cancellation.</param>
    /// <returns>The Orders page.</returns>
    Task<CustomerOrderPageSnapshot> ListCustomerOrdersAsync(uint limit, string? cursor, CancellationToken cancellationToken);

    /// <summary>Reads one order in the authenticated customer's scope.</summary>
    /// <param name="orderId">The requested order identifier.</param>
    /// <param name="cancellationToken">Call cancellation.</param>
    /// <returns>The Orders snapshot.</returns>
    Task<CustomerOrderSnapshot> GetCustomerOrderAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Reads one selected tenant's staff order page.</summary>
    /// <param name="tenantId">The authorized route tenant UUID.</param>
    /// <param name="limit">Positive page limit.</param>
    /// <param name="cursor">Optional opaque cursor.</param>
    /// <param name="cancellationToken">Call cancellation.</param>
    /// <returns>The Orders page.</returns>
    Task<AdminOrderPageSnapshot> ListAdminOrdersAsync(string tenantId, uint limit, string? cursor, CancellationToken cancellationToken);

    /// <summary>Reads one order in the selected staff tenant.</summary>
    /// <param name="tenantId">The authorized route tenant UUID.</param>
    /// <param name="orderId">The requested order identifier.</param>
    /// <param name="cancellationToken">Call cancellation.</param>
    /// <returns>The staff snapshot.</returns>
    Task<AdminOrderSnapshot> GetAdminOrderAsync(string tenantId, Guid orderId, CancellationToken cancellationToken);

    /// <summary>Records the first final decision for the selected tenant's order.</summary>
    /// <param name="tenantId">The authorized route tenant UUID.</param>
    /// <param name="orderId">The route order identifier.</param>
    /// <param name="decision">The staff decision.</param>
    /// <param name="reason">The trimmed staff reason.</param>
    /// <param name="expectedRevision">The observed order revision.</param>
    /// <param name="cancellationToken">Call cancellation.</param>
    /// <returns>The updated staff snapshot.</returns>
    Task<AdminOrderSnapshot> DecideOrderAsync(string tenantId, Guid orderId, AdminDecision decision, string reason, uint expectedRevision, CancellationToken cancellationToken);

    /// <summary>Records placement evidence for asynchronous internal verification.</summary>
    /// <param name="tenantId">The authorized route tenant UUID.</param>
    /// <param name="orderId">The route order identifier.</param>
    /// <param name="placementId">The allowlisted placement identifier.</param>
    /// <param name="evidenceReference">The nonsecret audit reference.</param>
    /// <param name="expectedRevision">The observed order revision.</param>
    /// <param name="cancellationToken">Call cancellation.</param>
    /// <returns>The updated staff snapshot.</returns>
    Task<AdminOrderSnapshot> RecordPlacementEvidenceAsync(string tenantId, Guid orderId, Guid placementId, string evidenceReference, uint expectedRevision, CancellationToken cancellationToken);
}
