using FastEndpoints;
using SharedKernel.Infrastructure.MultiTenancy;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Gateway.Models;
using Teck.Cloud.Commerce.Gateway.Serialization;
using Teck.Cloud.Commerce.Gateway.Services;

namespace Teck.Cloud.Commerce.CustomerGateway.Host.Endpoints;

/// <summary>GET /v1/commerce/catalog.</summary>
/// <param name="commerce">The customer commerce orchestration.</param>
public sealed class GetCatalogEndpoint(CustomerCommerceService commerce) : EndpointWithoutRequest<CatalogView>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Get("/v1/commerce/catalog");
        DontAutoSendResponse();
        Policies(CommerceAuth.CustomerPolicy);
        Options(builder => builder.WithMetadata(OperationScope.Tenant));
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await commerce.GetCatalogAsync(ct).ConfigureAwait(false);
        await CustomerEdge.WriteAsync(HttpContext, result, CommerceGatewayJsonContext.Default.CatalogView).ConfigureAwait(false);
    }
}
