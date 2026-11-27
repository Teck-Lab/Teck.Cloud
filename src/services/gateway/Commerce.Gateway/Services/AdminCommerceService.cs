using Grpc.Core;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Gateway.Clients;
using Teck.Cloud.Commerce.Gateway.Errors;
using Teck.Cloud.Commerce.Gateway.Models;

namespace Teck.Cloud.Commerce.Gateway.Services;

/// <summary>Orchestrates the staff edge over Orders only; no license, persistence or extra surface.</summary>
public sealed class AdminCommerceService
{
    private readonly ICommerceOrdersGateway _orders;

    /// <summary>Initializes a new instance of the <see cref="AdminCommerceService"/> class.</summary>
    /// <param name="orders">The Orders port.</param>
    public AdminCommerceService(ICommerceOrdersGateway orders)
    {
        ArgumentNullException.ThrowIfNull(orders);
        _orders = orders;
    }

    /// <summary>Reads one selected tenant's bounded staff page.</summary>
    /// <param name="tenantId">The authorized route tenant UUID.</param>
    /// <param name="limit">The bounded page size.</param>
    /// <param name="cursor">The original opaque cursor, if supplied.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The edge outcome.</returns>
    public Task<GatewayResult<AdminOrderPageView>> ListOrdersAsync(string tenantId, uint limit, string? cursor, CancellationToken cancellationToken) =>
        RunAsync(
            async () =>
            {
                var page = await _orders.ListAdminOrdersAsync(tenantId, limit, cursor, cancellationToken).ConfigureAwait(false);
                var views = page.Items
                    .OrderByDescending(order => order.UpdatedAt)
                    .ThenBy(order => order.OrderId.ToString("D"), StringComparer.Ordinal)
                    .Select(CommerceProtoMapper.ToView)
                    .ToArray();
                return new AdminOrderPageView(views, page.NextCursor);
            },
            cancellationToken);

    /// <summary>Reads one selected tenant's order.</summary>
    /// <param name="tenantId">The authorized route tenant UUID.</param>
    /// <param name="orderId">The route order identifier.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The edge outcome.</returns>
    public Task<GatewayResult<AdminOrderView>> GetOrderAsync(string tenantId, Guid orderId, CancellationToken cancellationToken) =>
        RunAsync(
            async () => CommerceProtoMapper.ToView(await _orders.GetAdminOrderAsync(tenantId, orderId, cancellationToken).ConfigureAwait(false)),
            cancellationToken);

    /// <summary>Records the first final decision for one selected tenant's order.</summary>
    /// <param name="tenantId">The authorized route tenant UUID.</param>
    /// <param name="orderId">The route order identifier.</param>
    /// <param name="decision">The staff decision.</param>
    /// <param name="reason">The trimmed nonblank reason.</param>
    /// <param name="expectedRevision">The observed order revision.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The edge outcome.</returns>
    public Task<GatewayResult<AdminOrderView>> DecideAsync(string tenantId, Guid orderId, AdminDecision decision, string reason, uint expectedRevision, CancellationToken cancellationToken) =>
        RunAsync(
            async () => CommerceProtoMapper.ToView(await _orders.DecideOrderAsync(tenantId, orderId, decision, reason.Trim(), expectedRevision, cancellationToken).ConfigureAwait(false)),
            cancellationToken);

    /// <summary>Records nonsecret placement evidence for asynchronous internal verification.</summary>
    /// <param name="tenantId">The authorized route tenant UUID.</param>
    /// <param name="orderId">The route order identifier.</param>
    /// <param name="placementId">The allowlisted placement identifier.</param>
    /// <param name="evidenceReference">The trimmed nonsecret audit reference.</param>
    /// <param name="expectedRevision">The observed order revision.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The edge outcome.</returns>
    public Task<GatewayResult<AdminOrderView>> RecordPlacementEvidenceAsync(string tenantId, Guid orderId, Guid placementId, string evidenceReference, uint expectedRevision, CancellationToken cancellationToken) =>
        RunAsync(
            async () => CommerceProtoMapper.ToView(await _orders.RecordPlacementEvidenceAsync(tenantId, orderId, placementId, evidenceReference.Trim(), expectedRevision, cancellationToken).ConfigureAwait(false)),
            cancellationToken);

    private static async Task<GatewayResult<T>> RunAsync<T>(Func<Task<T>> call, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            return GatewayResultFactory.Ok(await call().ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RpcException error)
        {
            return GatewayResultFactory.Fail<T>(CommerceProblemFactory.FromCode(CommerceRpc.ErrorCode(error)));
        }
        catch (CommerceDependencyException)
        {
            return GatewayResultFactory.Fail<T>(CommerceProblemFactory.DependencyUnavailable());
        }
    }
}
