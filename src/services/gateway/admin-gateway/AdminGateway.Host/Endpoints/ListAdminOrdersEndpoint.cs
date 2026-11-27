using FastEndpoints;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Infrastructure.MultiTenancy;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Gateway.Http;
using Teck.Cloud.Commerce.Gateway.Models;
using Teck.Cloud.Commerce.Gateway.Serialization;
using Teck.Cloud.Commerce.Gateway.Services;

namespace Teck.Cloud.Commerce.AdminGateway.Host.Endpoints;

/// <summary>GET /v1/admin/commerce/tenants/{tenantId}/orders.</summary>
/// <param name="commerce">The staff commerce orchestration.</param>
/// <param name="operation">The authenticated staff operation scope.</param>
public sealed class ListAdminOrdersEndpoint(AdminCommerceService commerce, OperationContext operation) : EndpointWithoutRequest<AdminOrderPageView>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Get("/v1/admin/commerce/tenants/{tenantId}/orders");
        DontAutoSendResponse();
        Policies(CommerceAuth.OrdersReadPolicy);
        Options(builder => builder.WithMetadata(OperationScope.AdministrativeTenant("tenantId", CommerceAuth.OrdersReadPolicy)));
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!CommerceRequestValidation.HasOnly(HttpContext.Request.Query, "limit", "cursor")
            || !CommerceRequestValidation.TryLimit(HttpContext.Request.Query["limit"], out var limit)
            || !CommerceRequestValidation.TryCursor(HttpContext.Request.Query["cursor"], out var cursor))
        {
            await AdminEdge.InvalidRequestAsync(HttpContext).ConfigureAwait(false);
            return;
        }

        var result = await commerce.ListOrdersAsync(AdminEdge.Tenant(operation), limit, cursor, ct).ConfigureAwait(false);
        await AdminEdge.WriteAsync(HttpContext, result, CommerceGatewayJsonContext.Default.AdminOrderPageView).ConfigureAwait(false);
    }
}
