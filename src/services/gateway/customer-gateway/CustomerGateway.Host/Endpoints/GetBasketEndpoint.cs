using FastEndpoints;
using SharedKernel.Infrastructure.MultiTenancy;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Gateway.Models;
using Teck.Cloud.Commerce.Gateway.Serialization;
using Teck.Cloud.Commerce.Gateway.Services;

namespace Teck.Cloud.Commerce.CustomerGateway.Host.Endpoints;

/// <summary>GET /v1/commerce/basket.</summary>
/// <param name="commerce">The customer commerce orchestration.</param>
public sealed class GetBasketEndpoint(CustomerCommerceService commerce) : EndpointWithoutRequest<BasketView>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Get("/v1/commerce/basket");
        DontAutoSendResponse();
        Policies(CommerceAuth.CustomerPolicy);
        Options(builder => builder.WithMetadata(OperationScope.Tenant));
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await commerce.GetBasketAsync(ct).ConfigureAwait(false);
        await CustomerEdge.WriteAsync(HttpContext, result, CommerceGatewayJsonContext.Default.BasketView).ConfigureAwait(false);
    }
}
