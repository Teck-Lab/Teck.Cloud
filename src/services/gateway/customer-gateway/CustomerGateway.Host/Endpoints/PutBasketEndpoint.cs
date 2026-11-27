using FastEndpoints;
using SharedKernel.Infrastructure.MultiTenancy;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Gateway.Http;
using Teck.Cloud.Commerce.Gateway.Models;
using Teck.Cloud.Commerce.Gateway.Serialization;
using Teck.Cloud.Commerce.Gateway.Services;

namespace Teck.Cloud.Commerce.CustomerGateway.Host.Endpoints;

/// <summary>PUT /v1/commerce/basket.</summary>
/// <param name="commerce">The customer commerce orchestration.</param>
public sealed class PutBasketEndpoint(CustomerCommerceService commerce) : EndpointWithoutRequest<BasketView>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Put("/v1/commerce/basket");
        DontAutoSendResponse();
        Policies(CommerceAuth.CustomerPolicy);
        Options(builder => builder.WithMetadata(OperationScope.Tenant));
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        var (read, body) = await CommerceBodyReader.TryReadAsync(HttpContext.Request, CommerceGatewayJsonContext.Default.PutBasketBody, ct).ConfigureAwait(false);
        if (!read || body is null || !CommerceRequestValidation.TryRevision(body.ExpectedRevision, 0, out var revision)
            || !CommerceRequestValidation.IsSelectionValid(body.Selection))
        {
            await CustomerEdge.InvalidRequestAsync(HttpContext).ConfigureAwait(false);
            return;
        }

        var requested = body.Selection;
        var selection = requested is null
            ? null
            : new BasketSelectionView(requested.OfferId!, [.. requested.AddOnCodes!]);
        var result = await commerce.PutBasketAsync(selection, revision, ct).ConfigureAwait(false);
        await CustomerEdge.WriteAsync(HttpContext, result, CommerceGatewayJsonContext.Default.BasketView).ConfigureAwait(false);
    }
}
