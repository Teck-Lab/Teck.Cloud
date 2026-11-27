using Grpc.Core;
using Microsoft.Extensions.Logging;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Gateway.Clients;
using Teck.Cloud.Commerce.Gateway.Errors;
using Teck.Cloud.Commerce.Gateway.Models;

namespace Teck.Cloud.Commerce.Gateway.Services;

/// <summary>Orchestrates the customer edge: fixed-order projection plus one phase-aware license resolution.</summary>
public sealed partial class CustomerCommerceService
{
    internal const string ActiveStatus = "active";

    private readonly ICommerceOrdersGateway _orders;
    private readonly ICommerceLicensingGateway _licensing;
    private readonly ILogger<CustomerCommerceService> _logger;

    /// <summary>Initializes a new instance of the <see cref="CustomerCommerceService"/> class.</summary>
    /// <param name="orders">The Orders port.</param>
    /// <param name="licensing">The Licensing port.</param>
    /// <param name="logger">The edge diagnostic log.</param>
    public CustomerCommerceService(ICommerceOrdersGateway orders, ICommerceLicensingGateway licensing, ILogger<CustomerCommerceService> logger)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(licensing);
        ArgumentNullException.ThrowIfNull(logger);
        _orders = orders;
        _licensing = licensing;
        _logger = logger;
    }

    /// <summary>Reads the catalog.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The edge outcome.</returns>
    public Task<GatewayResult<CatalogView>> GetCatalogAsync(CancellationToken cancellationToken) =>
        RunAsync(() => _orders.GetCatalogAsync(cancellationToken), cancellationToken);

    /// <summary>Reads the caller's basket.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The edge outcome.</returns>
    public Task<GatewayResult<BasketView>> GetBasketAsync(CancellationToken cancellationToken) =>
        RunAsync(() => _orders.GetBasketAsync(cancellationToken), cancellationToken);

    /// <summary>Replaces or clears the caller's basket.</summary>
    /// <param name="selection">The requested selection, or null to clear.</param>
    /// <param name="expectedRevision">The observed basket revision.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The edge outcome.</returns>
    public Task<GatewayResult<BasketView>> PutBasketAsync(BasketSelectionView? selection, uint expectedRevision, CancellationToken cancellationToken) =>
        RunAsync(() => _orders.PutBasketAsync(selection, expectedRevision, cancellationToken), cancellationToken);

    /// <summary>Reads the fixed customer page and resolves only each active item's own license.</summary>
    /// <param name="tenantId">The authenticated customer tenant UUID.</param>
    /// <param name="limit">The bounded page size.</param>
    /// <param name="cursor">The original opaque cursor, if supplied.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The edge outcome.</returns>
    public async Task<GatewayResult<OrderPageView>> ListOrdersAsync(string tenantId, uint limit, string? cursor, CancellationToken cancellationToken)
    {
        var page = await RunAsync(() => _orders.ListCustomerOrdersAsync(limit, cursor, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (!page.IsSuccess)
        {
            return GatewayResultFactory.Fail<OrderPageView>(page.Problem!);
        }

        var source = page.Value!;
        var resolved = new List<CustomerOrderSnapshot>(source.Items.Count);
        foreach (var item in source.Items)
        {
            var result = await ResolveAsync(item, tenantId, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return GatewayResultFactory.Fail<OrderPageView>(result.Problem!);
            }

            resolved.Add(result.Value!);
        }

        var views = resolved
            .OrderByDescending(order => order.UpdatedAt)
            .ThenBy(order => order.OrderId.ToString("D"), StringComparer.Ordinal)
            .Select(CommerceProtoMapper.ToView)
            .ToArray();
        return GatewayResultFactory.Ok(new OrderPageView(views, source.NextCursor));
    }

    /// <summary>Reads one own order and confirms its current license.</summary>
    /// <param name="tenantId">The authenticated customer tenant UUID.</param>
    /// <param name="orderId">The requested order identifier.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The edge outcome.</returns>
    public async Task<GatewayResult<CustomerOrderView>> GetOrderAsync(string tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        var order = await RunAsync(() => _orders.GetCustomerOrderAsync(orderId, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (!order.IsSuccess)
        {
            return GatewayResultFactory.Fail<CustomerOrderView>(order.Problem!);
        }

        var resolved = await ResolveAsync(order.Value!, tenantId, cancellationToken).ConfigureAwait(false);
        return resolved.IsSuccess
            ? GatewayResultFactory.Ok(CommerceProtoMapper.ToView(resolved.Value!))
            : GatewayResultFactory.Fail<CustomerOrderView>(resolved.Problem!);
    }

    /// <summary>Creates or replays one checkout intent without a second provider side effect.</summary>
    /// <param name="tenantId">The authenticated customer tenant UUID.</param>
    /// <param name="basketRevision">The observed basket revision.</param>
    /// <param name="idempotencyKey">The validated client idempotency UUID.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The edge outcome.</returns>
    public async Task<GatewayResult<CreateOrderView>> CreateOrderAsync(string tenantId, uint basketRevision, string idempotencyKey, CancellationToken cancellationToken)
    {
        var created = await RunAsync(() => _orders.CreateOrderAsync(basketRevision, idempotencyKey, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (!created.IsSuccess)
        {
            return GatewayResultFactory.Fail<CreateOrderView>(created.Problem!);
        }

        var createdOrder = created.Value!;
        var resolved = await ResolveAsync(createdOrder.Order, tenantId, cancellationToken).ConfigureAwait(false);
        return resolved.IsSuccess
            ? GatewayResultFactory.Ok(new CreateOrderView(CommerceProtoMapper.ToView(resolved.Value!), createdOrder.CheckoutUrl))
            : GatewayResultFactory.Fail<CreateOrderView>(resolved.Problem!);
    }

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

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "The license confirmation failed ({failureKind}); returning dependency-unavailable.")]
    private static partial void LogLicenseConfirmationFailed(ILogger logger, string failureKind);

    private async Task<GatewayResult<CustomerOrderSnapshot>> ResolveAsync(CustomerOrderSnapshot order, string tenantId, CancellationToken cancellationToken)
    {
        if (!string.Equals(order.Status, ActiveStatus, StringComparison.Ordinal))
        {
            return GatewayResultFactory.Ok(order with { License = null });
        }

        if (order.DesiredRevision is not > 0)
        {
            return GatewayResultFactory.Fail<CustomerOrderSnapshot>(CommerceProblemFactory.DependencyUnavailable());
        }

        try
        {
            var license = await _licensing.GetCurrentLicenseAsync(tenantId, order.OrderId, order.SubscriptionId, order.DesiredRevision.Value, cancellationToken).ConfigureAwait(false);
            return GatewayResultFactory.Ok(order with { License = license });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RpcException error) when (string.Equals(CommerceRpc.ErrorCode(error), CommerceErrorCodes.NotFound, StringComparison.Ordinal))
        {
            return await RereadAsync(order, tenantId, cancellationToken).ConfigureAwait(false);
        }
        catch (RpcException error)
        {
            return GatewayResultFactory.Fail<CustomerOrderSnapshot>(CommerceProblemFactory.FromCode(CommerceRpc.ErrorCode(error)));
        }
        catch (CommerceDependencyException)
        {
            return GatewayResultFactory.Fail<CustomerOrderSnapshot>(CommerceProblemFactory.DependencyUnavailable());
        }
    }

    private async Task<GatewayResult<CustomerOrderSnapshot>> RereadAsync(CustomerOrderSnapshot original, string tenantId, CancellationToken cancellationToken)
    {
        CustomerOrderSnapshot latest;
        try
        {
            latest = await _orders.GetCustomerOrderAsync(original.OrderId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RpcException error)
        {
            return GatewayResultFactory.Fail<CustomerOrderSnapshot>(CommerceProblemFactory.FromCode(CommerceRpc.ErrorCode(error)));
        }
        catch (CommerceDependencyException)
        {
            return GatewayResultFactory.Fail<CustomerOrderSnapshot>(CommerceProblemFactory.DependencyUnavailable());
        }

        if (latest.OrderId != original.OrderId)
        {
            return GatewayResultFactory.Fail<CustomerOrderSnapshot>(CommerceProblemFactory.DependencyUnavailable());
        }

        if (!string.Equals(latest.Status, ActiveStatus, StringComparison.Ordinal))
        {
            return GatewayResultFactory.Ok(latest with { License = null });
        }

        if (latest.DesiredRevision is not > 0)
        {
            return GatewayResultFactory.Fail<CustomerOrderSnapshot>(CommerceProblemFactory.DependencyUnavailable());
        }

        try
        {
            var license = await _licensing.GetCurrentLicenseAsync(tenantId, latest.OrderId, latest.SubscriptionId, latest.DesiredRevision.Value, cancellationToken).ConfigureAwait(false);
            return GatewayResultFactory.Ok(latest with { License = license });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // A dependency-side cancellation is not the caller's request, so it still fails closed.
            LogLicenseConfirmationFailed(_logger, "cancellation");
            return GatewayResultFactory.Fail<CustomerOrderSnapshot>(CommerceProblemFactory.DependencyUnavailable());
        }
        catch (Exception error)
        {
            // The confirmation phase fails closed for every non-OK, malformed OK and transport outcome.
            // Only the sanitized exception type is observed: messages can carry gRPC trailers, credentials, endpoints or documents.
            LogLicenseConfirmationFailed(_logger, error.GetType().Name);
            return GatewayResultFactory.Fail<CustomerOrderSnapshot>(CommerceProblemFactory.DependencyUnavailable());
        }
    }
}
