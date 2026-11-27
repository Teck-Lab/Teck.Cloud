using FastEndpoints;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Infrastructure.MultiTenancy;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Gateway.Http;
using Teck.Cloud.Commerce.Gateway.Models;
using Teck.Cloud.Commerce.Gateway.Serialization;
using Teck.Cloud.Commerce.Gateway.Services;

namespace Teck.Cloud.Commerce.CustomerGateway.Host.Endpoints;

/// <summary>GET /v1/commerce/orders.</summary>
/// <param name="commerce">The customer commerce orchestration.</param>
/// <param name="operation">The authenticated customer operation scope.</param>
public sealed class ListOrdersEndpoint(CustomerCommerceService commerce, OperationContext operation) : EndpointWithoutRequest<OrderPageView>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Get("/v1/commerce/orders");
        DontAutoSendResponse();
        Policies(CommerceAuth.CustomerPolicy);
        Options(builder => builder.WithMetadata(OperationScope.Tenant));
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!CommerceRequestValidation.HasOnly(HttpContext.Request.Query, "limit", "cursor")
            || !CommerceRequestValidation.TryLimit(HttpContext.Request.Query["limit"], out var limit)
            || !CommerceRequestValidation.TryCursor(HttpContext.Request.Query["cursor"], out var cursor))
        {
            await CustomerEdge.InvalidRequestAsync(HttpContext).ConfigureAwait(false);
            return;
        }

        var result = await commerce.ListOrdersAsync(CustomerEdge.Tenant(operation), limit, cursor, ct).ConfigureAwait(false);
        await CustomerEdge.WriteAsync(HttpContext, result, CommerceGatewayJsonContext.Default.OrderPageView).ConfigureAwait(false);
    }
}
