using FastEndpoints;
using SharedKernel.Core.MultiTenancy;
using SharedKernel.Infrastructure.MultiTenancy;
using Teck.Cloud.Commerce.Contracts;
using Teck.Cloud.Commerce.Gateway.Http;
using Teck.Cloud.Commerce.Gateway.Models;
using Teck.Cloud.Commerce.Gateway.Serialization;
using Teck.Cloud.Commerce.Gateway.Services;

namespace Teck.Cloud.Commerce.AdminGateway.Host.Endpoints;

/// <summary>POST /v1/admin/commerce/tenants/{tenantId}/orders/{orderId}/placement-evidence.</summary>
/// <param name="commerce">The staff commerce orchestration.</param>
/// <param name="operation">The authenticated staff operation scope.</param>
public sealed class RecordPlacementEvidenceEndpoint(AdminCommerceService commerce, OperationContext operation) : EndpointWithoutRequest<AdminOrderView>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Post("/v1/admin/commerce/tenants/{tenantId}/orders/{orderId}/placement-evidence");
        DontAutoSendResponse();
        Policies(CommerceAuth.PlacementRecordPolicy);
        Options(builder => builder.WithMetadata(OperationScope.AdministrativeTenant("tenantId", CommerceAuth.PlacementRecordPolicy)));
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(CancellationToken ct)
    {
        var (read, body) = await CommerceBodyReader.TryReadAsync(HttpContext.Request, CommerceGatewayJsonContext.Default.PlacementEvidenceBody, ct).ConfigureAwait(false);
        if (!CommerceRequestValidation.TryUuid(HttpContext.Request.RouteValues["orderId"] as string, out var orderId)
            || !read
            || body is null
            || !CommerceRequestValidation.TryUuid(body.PlacementId, out var placementId)
            || !CommerceRequestValidation.TryRevision(body.ExpectedRevision, 1, out var revision)
            || string.IsNullOrWhiteSpace(body.EvidenceReference)
            || body.EvidenceReference.Trim().Length > 512)
        {
            await AdminEdge.InvalidRequestAsync(HttpContext).ConfigureAwait(false);
            return;
        }

        var result = await commerce.RecordPlacementEvidenceAsync(AdminEdge.Tenant(operation), orderId, placementId, body.EvidenceReference, revision, ct).ConfigureAwait(false);
        await AdminEdge.WriteAsync(HttpContext, result, CommerceGatewayJsonContext.Default.AdminOrderView).ConfigureAwait(false);
    }
}
