// <copyright file="PatchCurrentTenantProfileEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Customer.Api.Infrastructure.MultiTenant;
using Customer.Application.Tenants.Features.PatchCurrentTenantProfile.V1;
using Customer.Application.Tenants.Features.UpdateTenantProfile.V1;
using Customer.Application.Tenants.Responses;
using ErrorOr;
using FastEndpoints;
using Finbuckle.MultiTenant.Abstractions;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.MultiTenant;
using SharedKernel.Infrastructure.OpenApi;

namespace Customer.Api.Endpoints.V1.Tenants.PatchCurrentTenantProfile;

public sealed class PatchCurrentTenantProfileEndpoint(
    ISender sender,
    IMultiTenantContextAccessor<TenantDetails>? tenantContextAccessor = null)
    : Endpoint<PatchCurrentTenantProfileRequest, TenantResponse>
{
    private readonly ISender sender = sender;
    private readonly IMultiTenantContextAccessor<TenantDetails>? tenantContextAccessor = tenantContextAccessor;

    public override void Configure()
    {
        Patch("/Tenants/me");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("tenant", "update");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
        Validator<PatchCurrentTenantProfileValidator>();
    }

    public override async Task HandleAsync(PatchCurrentTenantProfileRequest request, CancellationToken ct)
    {
        if (!CurrentTenantResolver.TryResolveTenantId(this.HttpContext, this.tenantContextAccessor, out Guid tenantId))
        {
            ErrorOr<TenantResponse> tenantResolutionError = Error.Validation(
                "Tenant.Context",
                "Current tenant context is missing or invalid");
            await this.SendAsync(tenantResolutionError, cancellation: ct).ConfigureAwait(false);
            return;
        }

        UpdateTenantProfileCommand command = new(tenantId, request.Name, request.Plan);
        ErrorOr<TenantResponse> response = await this.sender.Send(command, ct).ConfigureAwait(false);
        await this.SendAsync(response, cancellation: ct).ConfigureAwait(false);
    }
}
