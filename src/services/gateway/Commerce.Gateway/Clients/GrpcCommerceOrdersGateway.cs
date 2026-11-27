using Teck.Cloud.Commerce.Gateway.Composition;
using Teck.Cloud.Commerce.Gateway.Errors;
using Teck.Cloud.Commerce.Gateway.Models;
using OrdersV1 = Teck.Cloud.Commerce.Orders.V1;

namespace Teck.Cloud.Commerce.Gateway.Clients;

/// <summary>Maps the gateway port onto the generated Orders gRPC client.</summary>
public sealed class GrpcCommerceOrdersGateway : ICommerceOrdersGateway
{
    private readonly OrdersV1.Orders.OrdersClient _client;
    private readonly CommerceGatewayOptions _options;

    /// <summary>Initializes a new instance of the <see cref="GrpcCommerceOrdersGateway"/> class.</summary>
    /// <param name="client">The generated Orders client with exchanged call credentials.</param>
    /// <param name="options">The validated addresses and deadline.</param>
    public GrpcCommerceOrdersGateway(OrdersV1.Orders.OrdersClient client, CommerceGatewayOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        _client = client;
        _options = options;
    }

    /// <inheritdoc/>
    public async Task<CatalogView> GetCatalogAsync(CancellationToken cancellationToken) =>
        CommerceProtoMapper.ToCatalog(await CommerceGrpcCalls.UnaryAsync(
            _client.GetCatalogAsync(new OrdersV1.GetCatalogRequest(), deadline: Deadline(), cancellationToken: cancellationToken),
            cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public async Task<BasketView> GetBasketAsync(CancellationToken cancellationToken) =>
        CommerceProtoMapper.ToBasket(await CommerceGrpcCalls.UnaryAsync(
            _client.GetBasketAsync(new OrdersV1.GetBasketRequest(), deadline: Deadline(), cancellationToken: cancellationToken),
            cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public async Task<BasketView> PutBasketAsync(BasketSelectionView? selection, uint expectedRevision, CancellationToken cancellationToken)
    {
        var request = new OrdersV1.PutBasketRequest { ExpectedRevision = expectedRevision };
        if (selection is not null)
        {
            request.Selection = new OrdersV1.BasketSelection { OfferId = selection.OfferId };
            request.Selection.AddOnCodes.AddRange(selection.AddOnCodes);
        }

        return CommerceProtoMapper.ToBasket(await CommerceGrpcCalls.UnaryAsync(
            _client.PutBasketAsync(request, deadline: Deadline(), cancellationToken: cancellationToken),
            cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task<CreateOrderSnapshot> CreateOrderAsync(uint basketRevision, string idempotencyKey, CancellationToken cancellationToken)
    {
        var response = await CommerceGrpcCalls.UnaryAsync(
            _client.CreateOrderAsync(
                new OrdersV1.CreateOrderRequest { BasketRevision = basketRevision, IdempotencyKey = idempotencyKey },
                deadline: Deadline(),
                cancellationToken: cancellationToken),
            cancellationToken).ConfigureAwait(false);
        var order = response.Order ?? throw new CommerceDependencyException("Orders returned a create response without an order.");
        return new CreateOrderSnapshot(
            CommerceProtoMapper.ToCustomerSnapshot(order),
            Nullable(response.HasCheckoutUrl, response.CheckoutUrl));
    }

    /// <inheritdoc/>
    public async Task<CustomerOrderPageSnapshot> ListCustomerOrdersAsync(uint limit, string? cursor, CancellationToken cancellationToken)
    {
        var request = new OrdersV1.ListCustomerOrdersRequest { Limit = limit };
        if (cursor is not null)
        {
            request.Cursor = cursor;
        }

        var response = await CommerceGrpcCalls.UnaryAsync(
            _client.ListCustomerOrdersAsync(request, deadline: Deadline(), cancellationToken: cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return new CustomerOrderPageSnapshot(
            [.. response.Items.Select(CommerceProtoMapper.ToCustomerSnapshot)],
            Cursor(response.HasNextCursor, response.NextCursor));
    }

    /// <inheritdoc/>
    public async Task<CustomerOrderSnapshot> GetCustomerOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var response = await CommerceGrpcCalls.UnaryAsync(
            _client.GetCustomerOrderAsync(
                new OrdersV1.GetCustomerOrderRequest { OrderId = orderId.ToString("D") },
                deadline: Deadline(),
                cancellationToken: cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return CommerceProtoMapper.ToCustomerSnapshot(response.Order ?? throw new CommerceDependencyException("Orders returned a customer detail response without an order."));
    }

    /// <inheritdoc/>
    public Task<AdminOrderPageSnapshot> ListAdminOrdersAsync(string tenantId, uint limit, string? cursor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        return ListAdminOrdersCoreAsync(limit, cursor, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<AdminOrderSnapshot> GetAdminOrderAsync(string tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        return GetAdminOrderCoreAsync(orderId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<AdminOrderSnapshot> DecideOrderAsync(string tenantId, Guid orderId, AdminDecision decision, string reason, uint expectedRevision, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        return DecideOrderCoreAsync(orderId, decision, reason, expectedRevision, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<AdminOrderSnapshot> RecordPlacementEvidenceAsync(string tenantId, Guid orderId, Guid placementId, string evidenceReference, uint expectedRevision, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        return RecordPlacementEvidenceCoreAsync(orderId, placementId, evidenceReference, expectedRevision, cancellationToken);
    }

    private static string? Cursor(bool present, string value) =>
        present ? RequiredCursor(value) : null;

    private static string RequiredCursor(string value) =>
        value.Length == 0 ? throw new CommerceDependencyException("Orders returned an empty page cursor.") : value;

    private static string? Nullable(bool present, string value) =>
        present ? RequiredCheckoutUrl(value) : null;

    private static string RequiredCheckoutUrl(string value) =>
        value.Length == 0 ? throw new CommerceDependencyException("Orders returned an empty checkout URL.") : value;

    private async Task<AdminOrderPageSnapshot> ListAdminOrdersCoreAsync(uint limit, string? cursor, CancellationToken cancellationToken)
    {
        var request = new OrdersV1.ListAdminOrdersRequest { Limit = limit };
        if (cursor is not null)
        {
            request.Cursor = cursor;
        }

        var response = await CommerceGrpcCalls.UnaryAsync(
            _client.ListAdminOrdersAsync(request, deadline: Deadline(), cancellationToken: cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return new AdminOrderPageSnapshot(
            [.. response.Items.Select(CommerceProtoMapper.ToAdminSnapshot)],
            Cursor(response.HasNextCursor, response.NextCursor));
    }

    private async Task<AdminOrderSnapshot> GetAdminOrderCoreAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var response = await CommerceGrpcCalls.UnaryAsync(
            _client.GetAdminOrderAsync(
                new OrdersV1.GetAdminOrderRequest { OrderId = orderId.ToString("D") },
                deadline: Deadline(),
                cancellationToken: cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return CommerceProtoMapper.ToAdminSnapshot(response.Order ?? throw new CommerceDependencyException("Orders returned an admin detail response without an order."));
    }

    private async Task<AdminOrderSnapshot> DecideOrderCoreAsync(Guid orderId, AdminDecision decision, string reason, uint expectedRevision, CancellationToken cancellationToken)
    {
        var response = await CommerceGrpcCalls.UnaryAsync(
            _client.DecideOrderAsync(
                new OrdersV1.DecideOrderRequest
                {
                    Decision = decision == AdminDecision.Approve ? OrdersV1.Decision.Approve : OrdersV1.Decision.Reject,
                    Reason = reason,
                    ExpectedRevision = expectedRevision,
                    OrderId = orderId.ToString("D"),
                },
                deadline: Deadline(),
                cancellationToken: cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return CommerceProtoMapper.ToAdminSnapshot(response.Order ?? throw new CommerceDependencyException("Orders returned a decision response without an order."));
    }

    private async Task<AdminOrderSnapshot> RecordPlacementEvidenceCoreAsync(Guid orderId, Guid placementId, string evidenceReference, uint expectedRevision, CancellationToken cancellationToken)
    {
        var response = await CommerceGrpcCalls.UnaryAsync(
            _client.RecordPlacementEvidenceAsync(
                new OrdersV1.RecordPlacementEvidenceRequest
                {
                    PlacementId = placementId.ToString("D"),
                    EvidenceReference = evidenceReference,
                    ExpectedRevision = expectedRevision,
                    OrderId = orderId.ToString("D"),
                },
                deadline: Deadline(),
                cancellationToken: cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return CommerceProtoMapper.ToAdminSnapshot(response.Order ?? throw new CommerceDependencyException("Orders returned a placement-evidence response without an order."));
    }

    private DateTime Deadline() => DateTime.UtcNow.Add(_options.RequestTimeout);
}
