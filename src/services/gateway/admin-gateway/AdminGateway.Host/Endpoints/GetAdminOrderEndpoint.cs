using FastEndpoints;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Infrastructure.MultiTenancy;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Gateway.Http;
using Teck.Cloud.Commerce.Gateway.Models;
using Teck.Cloud.Commerce.Gateway.Serialization;
using Teck.Cloud.Commerce.Gateway.Services;

namespace Teck.Cloud.Commerce.AdminGateway.Host.Endpoints;

/// <summary>GET /v1/admin/commerce/tenants/{tenantId}/orders/{orderId}.</summary>
/// <param name="commerce">The staff commerce orchestration.</param>
/// <param name="operation">The authenticated staff operation scope.</param>
public sealed class GetAdminOrderEndpoint(AdminCommerceService commerce, OperationContext operation) : EndpointWithoutRequest<AdminOrderView>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Get("/v1/admin/commerce/tenants/{tenantId}/orders/{orderId}");
        DontAutoSendResponse();
        Policies(CommerceAuth.OrdersReadPolicy);
        Options(builder => builder.WithMetadata(OperationScope.AdministrativeTenant("tenantId", CommerceAuth.OrdersReadPolicy)));
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!CommerceRequestValidation.TryUuid(HttpContext.Request.RouteValues["orderId"] as string, out var orderId))
        {
            await AdminEdge.InvalidRequestAsync(HttpContext).ConfigureAwait(false);
            return;
        }

        var result = await commerce.GetOrderAsync(AdminEdge.Tenant(operation), orderId, ct).ConfigureAwait(false);
        await AdminEdge.WriteAsync(HttpContext, result, CommerceGatewayJsonContext.Default.AdminOrderView).ConfigureAwait(false);
    }
}
