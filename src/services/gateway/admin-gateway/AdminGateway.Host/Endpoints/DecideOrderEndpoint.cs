using FastEndpoints;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Infrastructure.MultiTenancy;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Gateway.Clients;
using Teck.Cloud.Commerce.Gateway.Http;
using Teck.Cloud.Commerce.Gateway.Models;
using Teck.Cloud.Commerce.Gateway.Serialization;
using Teck.Cloud.Commerce.Gateway.Services;

namespace Teck.Cloud.Commerce.AdminGateway.Host.Endpoints;

/// <summary>POST /v1/admin/commerce/tenants/{tenantId}/orders/{orderId}/decision.</summary>
/// <param name="commerce">The staff commerce orchestration.</param>
/// <param name="operation">The authenticated staff operation scope.</param>
public sealed class DecideOrderEndpoint(AdminCommerceService commerce, OperationContext operation) : EndpointWithoutRequest<AdminOrderView>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Post("/v1/admin/commerce/tenants/{tenantId}/orders/{orderId}/decision");
        DontAutoSendResponse();
        Policies(CommerceAuth.OrdersDecidePolicy);
        Options(builder => builder.WithMetadata(OperationScope.AdministrativeTenant("tenantId", CommerceAuth.OrdersDecidePolicy)));
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        var (read, body) = await CommerceBodyReader.TryReadAsync(HttpContext.Request, CommerceGatewayJsonContext.Default.DecisionBody, ct).ConfigureAwait(false);
        if (!CommerceRequestValidation.TryUuid(HttpContext.Request.RouteValues["orderId"] as string, out var orderId)
            || !read
            || body is null
            || !CommerceRequestValidation.TryRevision(body.ExpectedRevision, 1, out var revision)
            || (body.Decision is not ("approve" or "reject"))
            || string.IsNullOrWhiteSpace(body.Reason)
            || body.Reason.Trim().Length > 512)
        {
            await AdminEdge.InvalidRequestAsync(HttpContext).ConfigureAwait(false);
            return;
        }

        var decision = string.Equals(body.Decision, "approve", StringComparison.Ordinal) ? AdminDecision.Approve : AdminDecision.Reject;
        var result = await commerce.DecideAsync(AdminEdge.Tenant(operation), orderId, decision, body.Reason, revision, ct).ConfigureAwait(false);
        await AdminEdge.WriteAsync(HttpContext, result, CommerceGatewayJsonContext.Default.AdminOrderView).ConfigureAwait(false);
    }
}
