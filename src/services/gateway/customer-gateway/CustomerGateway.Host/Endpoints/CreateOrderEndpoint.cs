using FastEndpoints;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Infrastructure.MultiTenancy;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Gateway.Http;
using Teck.Cloud.Commerce.Gateway.Models;
using Teck.Cloud.Commerce.Gateway.Serialization;
using Teck.Cloud.Commerce.Gateway.Services;

namespace Teck.Cloud.Commerce.CustomerGateway.Host.Endpoints;

/// <summary>POST /v1/commerce/orders.</summary>
/// <param name="commerce">The customer commerce orchestration.</param>
/// <param name="operation">The authenticated customer operation scope.</param>
public sealed class CreateOrderEndpoint(CustomerCommerceService commerce, OperationContext operation) : EndpointWithoutRequest<CreateOrderView>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Post("/v1/commerce/orders");
        DontAutoSendResponse();
        Policies(CommerceAuth.CustomerPolicy);
        Options(builder => builder.WithMetadata(OperationScope.Tenant));
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        var idempotencyKey = CommerceBodyReader.SingleHeader(HttpContext.Request, "Idempotency-Key");
        var (read, body) = await CommerceBodyReader.TryReadAsync(HttpContext.Request, CommerceGatewayJsonContext.Default.CreateOrderBody, ct).ConfigureAwait(false);
        if (!CommerceRequestValidation.TryUuid(idempotencyKey, out var key)
            || !read
            || body is null
            || !CommerceRequestValidation.TryRevision(body.BasketRevision, 0, out var revision))
        {
            await CustomerEdge.InvalidRequestAsync(HttpContext).ConfigureAwait(false);
            return;
        }

        var result = await commerce.CreateOrderAsync(CustomerEdge.Tenant(operation), revision, key.ToString("D"), ct).ConfigureAwait(false);
        await CustomerEdge.WriteAsync(HttpContext, result, CommerceGatewayJsonContext.Default.CreateOrderView, StatusCodes.Status201Created).ConfigureAwait(false);
    }
}
