// <copyright file="GetCurrentTenantDatabaseInfoEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Customer.Api.Infrastructure.MultiTenant;
using Customer.Application.Tenants.Features.GetCurrentTenantDatabaseInfo.V1;
using ErrorOr;
using FastEndpoints;
using Finbuckle.MultiTenant.Abstractions;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.MultiTenant;
using SharedKernel.Infrastructure.OpenApi;

namespace Customer.Api.Endpoints.V1.Tenants.GetCurrentTenantDatabaseInfo;

public sealed class GetCurrentTenantDatabaseInfoEndpoint(
    ISender sender,
    IMultiTenantContextAccessor<TenantDetails>? tenantContextAccessor = null)
    : Endpoint<GetCurrentTenantDatabaseInfoRequest, GetCurrentTenantDatabaseInfoResponse>
{
    private readonly ISender sender = sender;
    private readonly IMultiTenantContextAccessor<TenantDetails>? tenantContextAccessor = tenantContextAccessor;

    public override void Configure()
    {
        Get("/Tenants/me/database-info");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("tenant", "list");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
    }

    public override async Task HandleAsync(GetCurrentTenantDatabaseInfoRequest request, CancellationToken ct)
    {
        if (!CurrentTenantResolver.TryResolveTenantId(this.HttpContext, this.tenantContextAccessor, out Guid tenantId))
        {
            ErrorOr<GetCurrentTenantDatabaseInfoResponse> tenantResolutionError = Error.Validation(
                "Tenant.Context",
                "Current tenant context is missing or invalid");
            await this.SendAsync(tenantResolutionError, cancellation: ct).ConfigureAwait(false);
            return;
        }

        string serviceName = string.IsNullOrWhiteSpace(request.ServiceName)
            ? "customer"
            : request.ServiceName;

        GetCurrentTenantDatabaseInfoQuery query = new(tenantId, serviceName);
        ErrorOr<GetCurrentTenantDatabaseInfoResponse> queryResponse = await this.sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(queryResponse, cancellation: ct).ConfigureAwait(false);
    }
}
