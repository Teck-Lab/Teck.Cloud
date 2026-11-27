using ErrorOr;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Core.MultiTenancy;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Orders.Application.Baskets.Features.GetBasket.V1;
using Teck.Cloud.Commerce.Orders.Application.Baskets.Features.PutBasket.V1;
using Teck.Cloud.Commerce.Orders.Application.Catalog.Features.GetCatalog.V1;
using Teck.Cloud.Commerce.Orders.Application.Orders.Features.CheckCurrentEntitlement.V1;
using Teck.Cloud.Commerce.Orders.Application.Orders.Features.CreateOrder.V1;
using Teck.Cloud.Commerce.Orders.Application.Orders.Features.DecideOrder.V1;
using Teck.Cloud.Commerce.Orders.Application.Orders.Features.GetAdminOrder.V1;
using Teck.Cloud.Commerce.Orders.Application.Orders.Features.GetCustomerOrder.V1;
using Teck.Cloud.Commerce.Orders.Application.Orders.Features.ListAdminOrders.V1;
using Teck.Cloud.Commerce.Orders.Application.Orders.Features.ListCustomerOrders.V1;
using Teck.Cloud.Commerce.Orders.Application.Orders.Features.RecordPlacementEvidence.V1;
using Teck.Cloud.Commerce.Orders.Application.Orders.Responses;
using Teck.Cloud.Commerce.Orders.Domain.Common;
using Wolverine;

namespace Teck.Cloud.Commerce.Orders.Host.Grpc;

/// <summary>Inbound gRPC adapter; it dispatches the same use cases without owning state.</summary>
/// <param name="bus">The local message bus.</param>
/// <param name="operation">The interceptor-established operation identity.</param>
public sealed class OrdersGrpcService(IMessageBus bus, OperationContext operation) : V1.Orders.OrdersBase
{
    /// <inheritdoc/>
    [Authorize(Policy = CommerceAuth.CustomerPolicy)]
    public override async Task<V1.GetCatalogResponse> GetCatalog(V1.GetCatalogRequest request, ServerCallContext context)
    {
        var tenant = RequireTenant();
        var result = await bus.InvokeForTenantAsync<ErrorOr<IReadOnlyList<OfferDto>>>(
            tenant,
            new GetCatalogQuery(tenant),
            context.CancellationToken).ConfigureAwait(false);
        if (result.IsError)
        {
            throw CommerceRpc.Error(result.FirstError.Code);
        }

        var response = new V1.GetCatalogResponse();
        response.Offers.AddRange(result.Value.Select(ProtoMapping.Offer));
        return response;
    }

    /// <inheritdoc/>
    [Authorize(Policy = CommerceAuth.CustomerPolicy)]
    public override async Task<V1.Basket> GetBasket(V1.GetBasketRequest request, ServerCallContext context)
    {
        var tenant = RequireTenant();
        var subject = RequireSubject();
        var result = await bus.InvokeForTenantAsync<ErrorOr<BasketDto>>(
            tenant,
            new GetBasketQuery(tenant, subject),
            context.CancellationToken).ConfigureAwait(false);
        return result.IsError ? throw CommerceRpc.Error(result.FirstError.Code) : ProtoMapping.Basket(result.Value);
    }

    /// <inheritdoc/>
    [Authorize(Policy = CommerceAuth.CustomerPolicy)]
    public override Task<V1.Basket> PutBasket(V1.PutBasketRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PutBasketAsync(request, context);
    }

    /// <inheritdoc/>
    [Authorize(Policy = CommerceAuth.CustomerPolicy)]
    public override Task<V1.CreateOrderResponse> CreateOrder(V1.CreateOrderRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CreateOrderAsync(request, context);
    }

    /// <inheritdoc/>
    [Authorize(Policy = CommerceAuth.CustomerPolicy)]
    public override Task<V1.ListCustomerOrdersResponse> ListCustomerOrders(V1.ListCustomerOrdersRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ListCustomerOrdersAsync(request, context);
    }

    /// <inheritdoc/>
    [Authorize(Policy = CommerceAuth.CustomerPolicy)]
    public override Task<V1.GetCustomerOrderResponse> GetCustomerOrder(V1.GetCustomerOrderRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GetCustomerOrderAsync(request, context);
    }

    /// <inheritdoc/>
    [Authorize(Policy = CommerceAuth.OrdersReadPolicy)]
    public override Task<V1.ListAdminOrdersResponse> ListAdminOrders(V1.ListAdminOrdersRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ListAdminOrdersAsync(request, context);
    }

    /// <inheritdoc/>
    [Authorize(Policy = CommerceAuth.OrdersReadPolicy)]
    public override Task<V1.GetAdminOrderResponse> GetAdminOrder(V1.GetAdminOrderRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GetAdminOrderAsync(request, context);
    }

    /// <inheritdoc/>
    [Authorize(Policy = CommerceAuth.OrdersDecidePolicy)]
    public override Task<V1.DecideOrderResponse> DecideOrder(V1.DecideOrderRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DecideOrderAsync(request, context);
    }

    /// <inheritdoc/>
    [Authorize(Policy = CommerceAuth.PlacementRecordPolicy)]
    public override Task<V1.RecordPlacementEvidenceResponse> RecordPlacementEvidence(
        V1.RecordPlacementEvidenceRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RecordPlacementEvidenceAsync(request, context);
    }

    /// <inheritdoc/>
    [Authorize(Policy = CommerceAuth.EntitlementCheckPolicy)]
    public override Task<V1.CheckCurrentEntitlementResponse> CheckCurrentEntitlement(
        V1.CheckCurrentEntitlementRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CheckCurrentEntitlementAsync(request, context);
    }

    private async Task<V1.Basket> PutBasketAsync(V1.PutBasketRequest request, ServerCallContext context)
    {
        var tenant = RequireTenant();
        var subject = RequireSubject();
        var command = new PutBasketCommand(
            tenant,
            subject,
            request.Selection?.OfferId,
            request.Selection?.AddOnCodes.ToArray() ?? [],
            request.ExpectedRevision);
        var result = await InvokeAsync<BasketDto>(tenant, command, context.CancellationToken).ConfigureAwait(false)
            ?? throw CommerceRpc.Error(CommerceErrorCodes.StaleRevision);
        return result.IsError ? throw CommerceRpc.Error(result.FirstError.Code) : ProtoMapping.Basket(result.Value);
    }

    private async Task<V1.CreateOrderResponse> CreateOrderAsync(V1.CreateOrderRequest request, ServerCallContext context)
    {
        var tenant = RequireTenant();
        var subject = RequireSubject();
        var command = new CreateOrderCommand(tenant, subject, request.BasketRevision, request.IdempotencyKey);
        var result = await InvokeAsync<CheckoutResultDto>(tenant, command, context.CancellationToken).ConfigureAwait(false)
            ?? throw CommerceRpc.Error(CommerceErrorCodes.StaleRevision);
        if (result.IsError)
        {
            throw CommerceRpc.Error(result.FirstError.Code);
        }

        var response = new V1.CreateOrderResponse { Order = ProtoMapping.Customer(result.Value.Order) };
        if (result.Value.CheckoutUrl is { } url)
        {
            response.CheckoutUrl = url;
        }
        else
        {
            response.ClearCheckoutUrl();
        }

        return response;
    }

    private async Task<V1.ListCustomerOrdersResponse> ListCustomerOrdersAsync(
        V1.ListCustomerOrdersRequest request,
        ServerCallContext context)
    {
        var tenant = RequireTenant();
        var subject = RequireSubject();
        var result = await bus.InvokeForTenantAsync<ErrorOr<PagedResultDto<CustomerOrderDto>>>(
            tenant,
            new ListCustomerOrdersQuery(tenant, subject, request.Limit, request.HasCursor ? request.Cursor : null),
            context.CancellationToken).ConfigureAwait(false);
        if (result.IsError)
        {
            throw CommerceRpc.Error(result.FirstError.Code);
        }

        var response = new V1.ListCustomerOrdersResponse();
        response.Items.AddRange(result.Value.Items.Select(ProtoMapping.Customer));
        if (result.Value.NextCursor is { } cursor)
        {
            response.NextCursor = cursor;
        }

        return response;
    }

    private async Task<V1.GetCustomerOrderResponse> GetCustomerOrderAsync(
        V1.GetCustomerOrderRequest request,
        ServerCallContext context)
    {
        var tenant = RequireTenant();
        var subject = RequireSubject();
        if (!Guid.TryParseExact(request.OrderId, "D", out var orderId))
        {
            throw CommerceRpc.Error(CommerceErrorCodes.InvalidRequest);
        }

        var result = await bus.InvokeForTenantAsync<ErrorOr<CustomerOrderDto>>(
            tenant,
            new GetCustomerOrderQuery(tenant, subject, orderId),
            context.CancellationToken).ConfigureAwait(false);
        return result.IsError
            ? throw CommerceRpc.Error(result.FirstError.Code)
            : new V1.GetCustomerOrderResponse { Order = ProtoMapping.Customer(result.Value) };
    }

    private async Task<V1.ListAdminOrdersResponse> ListAdminOrdersAsync(
        V1.ListAdminOrdersRequest request,
        ServerCallContext context)
    {
        var tenant = RequireTenant();
        var result = await bus.InvokeForTenantAsync<ErrorOr<PagedResultDto<AdminOrderDto>>>(
            tenant,
            new ListAdminOrdersQuery(tenant, request.Limit, request.HasCursor ? request.Cursor : null),
            context.CancellationToken).ConfigureAwait(false);
        if (result.IsError)
        {
            throw CommerceRpc.Error(result.FirstError.Code);
        }

        var response = new V1.ListAdminOrdersResponse();
        response.Items.AddRange(result.Value.Items.Select(ProtoMapping.Admin));
        if (result.Value.NextCursor is { } cursor)
        {
            response.NextCursor = cursor;
        }

        return response;
    }

    private async Task<V1.GetAdminOrderResponse> GetAdminOrderAsync(
        V1.GetAdminOrderRequest request,
        ServerCallContext context)
    {
        var tenant = RequireTenant();
        if (!Guid.TryParseExact(request.OrderId, "D", out var orderId))
        {
            throw CommerceRpc.Error(CommerceErrorCodes.InvalidRequest);
        }

        var result = await bus.InvokeForTenantAsync<ErrorOr<AdminOrderDto>>(
            tenant,
            new GetAdminOrderQuery(tenant, orderId),
            context.CancellationToken).ConfigureAwait(false);
        return result.IsError
            ? throw CommerceRpc.Error(result.FirstError.Code)
            : new V1.GetAdminOrderResponse { Order = ProtoMapping.Admin(result.Value) };
    }

    private async Task<V1.DecideOrderResponse> DecideOrderAsync(V1.DecideOrderRequest request, ServerCallContext context)
    {
        var tenant = RequireTenant();
        var actor = RequireActor();
        if (!Guid.TryParseExact(request.OrderId, "D", out var orderId))
        {
            throw CommerceRpc.Error(CommerceErrorCodes.InvalidRequest);
        }

        var decision = request.Decision switch
        {
            V1.Decision.Approve => Decision.Approve,
            V1.Decision.Reject => Decision.Reject,
            V1.Decision.Unspecified => throw CommerceRpc.Error(CommerceErrorCodes.InvalidRequest),
            _ => throw CommerceRpc.Error(CommerceErrorCodes.InvalidRequest),
        };
        var result = await InvokeAsync<AdminOrderDto>(
                tenant,
                new DecideOrderCommand(tenant, orderId, decision, request.Reason, request.ExpectedRevision, actor),
                context.CancellationToken)
            .ConfigureAwait(false)
            ?? throw CommerceRpc.Error(CommerceErrorCodes.StaleRevision);
        return result.IsError
            ? throw CommerceRpc.Error(result.FirstError.Code)
            : new V1.DecideOrderResponse { Order = ProtoMapping.Admin(result.Value) };
    }

    private async Task<V1.RecordPlacementEvidenceResponse> RecordPlacementEvidenceAsync(
        V1.RecordPlacementEvidenceRequest request,
        ServerCallContext context)
    {
        var tenant = RequireTenant();
        var actor = RequireActor();
        if (!Guid.TryParseExact(request.OrderId, "D", out var orderId))
        {
            throw CommerceRpc.Error(CommerceErrorCodes.InvalidRequest);
        }

        var result = await InvokeAsync<AdminOrderDto>(
                tenant,
                new RecordPlacementEvidenceCommand(
                    tenant,
                    orderId,
                    request.PlacementId,
                    request.EvidenceReference,
                    request.ExpectedRevision,
                    actor),
                context.CancellationToken)
            .ConfigureAwait(false)
            ?? throw CommerceRpc.Error(CommerceErrorCodes.StaleRevision);
        return result.IsError
            ? throw CommerceRpc.Error(result.FirstError.Code)
            : new V1.RecordPlacementEvidenceResponse { Order = ProtoMapping.Admin(result.Value) };
    }

    private async Task<V1.CheckCurrentEntitlementResponse> CheckCurrentEntitlementAsync(
        V1.CheckCurrentEntitlementRequest request,
        ServerCallContext context)
    {
        var tenant = RequireTenant();
        if (!string.Equals(request.TenantId, tenant, StringComparison.Ordinal)
            || !Guid.TryParseExact(request.OrderId, "D", out var orderId)
            || !Guid.TryParseExact(request.SubscriptionId, "D", out var subscriptionId))
        {
            throw CommerceRpc.Error(CommerceErrorCodes.InvalidRequest);
        }

        var result = await bus.InvokeForTenantAsync<ErrorOr<EntitlementCheckDto>>(
            tenant,
            new CheckCurrentEntitlementQuery(tenant, orderId, subscriptionId, request.DesiredRevision),
            context.CancellationToken).ConfigureAwait(false);
        return result.IsError
            ? throw CommerceRpc.Error(result.FirstError.Code)
            : new V1.CheckCurrentEntitlementResponse { Eligible = result.Value.Eligible };
    }

    private async Task<ErrorOr<T>?> InvokeAsync<T>(string tenantId, object message, CancellationToken cancellationToken)
    {
        try
        {
            return await bus.InvokeForTenantAsync<ErrorOr<T>>(tenantId, message, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }
    }

    private string RequireTenant()
    {
        string? tenantId;
        try
        {
            tenantId = operation.Scope.TenantId;
        }
        catch (InvalidOperationException)
        {
            tenantId = null;
        }

        return tenantId ?? throw CommerceRpc.Error(CommerceErrorCodes.Forbidden);
    }

    private string RequireSubject() =>
        operation.ActorId ?? throw CommerceRpc.Error(CommerceErrorCodes.Forbidden);

    private string RequireActor() =>
        operation.ActorId ?? throw CommerceRpc.Error(CommerceErrorCodes.Forbidden);
}
