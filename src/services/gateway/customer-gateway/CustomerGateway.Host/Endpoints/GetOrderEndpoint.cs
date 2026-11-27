using FastEndpoints;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Infrastructure.MultiTenancy;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Gateway.Http;
using Teck.Cloud.Commerce.Gateway.Models;
using Teck.Cloud.Commerce.Gateway.Serialization;
using Teck.Cloud.Commerce.Gateway.Services;

namespace Teck.Cloud.Commerce.CustomerGateway.Host.Endpoints;

/// <summary>GET /v1/commerce/orders/{orderId}.</summary>
/// <param name="commerce">The customer commerce orchestration.</param>
/// <param name="operation">The authenticated customer operation scope.</param>
public sealed class GetOrderEndpoint(CustomerCommerceService commerce, OperationContext operation) : EndpointWithoutRequest<CustomerOrderView>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Get("/v1/commerce/orders/{orderId}");
        DontAutoSendResponse();
        Policies(CommerceAuth.CustomerPolicy);
        Options(builder => builder.WithMetadata(OperationScope.Tenant));
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!CommerceRequestValidation.TryUuid(HttpContext.Request.RouteValues["orderId"] as string, out var orderId))
        {
            await CustomerEdge.InvalidRequestAsync(HttpContext).ConfigureAwait(false);
            return;
        }

        var result = await commerce.GetOrderAsync(CustomerEdge.Tenant(operation), orderId, ct).ConfigureAwait(false);
        await CustomerEdge.WriteAsync(HttpContext, result, CommerceGatewayJsonContext.Default.CustomerOrderView).ConfigureAwait(false);
    }
}
