// <copyright file="CheckCurrentTenantServiceReadinessEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Customer.Api.Infrastructure.MultiTenant;
using Customer.Application.Tenants.Features.CheckServiceReadiness.V1;
using Customer.Application.Tenants.Features.GetTenantById.V1;
using Customer.Application.Tenants.Responses;
using ErrorOr;
using FastEndpoints;
using Finbuckle.MultiTenant.Abstractions;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.MultiTenant;
using SharedKernel.Infrastructure.OpenApi;

namespace Customer.Api.Endpoints.V1.Tenants.CheckServiceReadiness;

public sealed class CheckCurrentTenantServiceReadinessEndpoint(
    ISender sender,
    IMultiTenantContextAccessor<TenantDetails>? tenantContextAccessor = null)
    : Endpoint<CheckCurrentTenantServiceReadinessRequest, ServiceReadinessResponse>
{
    private readonly ISender sender = sender;
    private readonly IMultiTenantContextAccessor<TenantDetails>? tenantContextAccessor = tenantContextAccessor;

    public override void Configure()
    {
        Get("/Tenants/me/Services/{ServiceName}/Readiness");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("tenant", "list");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
    }

    public override async Task HandleAsync(CheckCurrentTenantServiceReadinessRequest request, CancellationToken ct)
    {
        if (!CurrentTenantResolver.TryResolveTenantId(this.HttpContext, this.tenantContextAccessor, out Guid tenantId))
        {
            ErrorOr<ServiceReadinessResponse> tenantResolutionError = Error.Validation(
                "Tenant.Context",
                "Current tenant context is missing or invalid");
            await this.SendAsync(tenantResolutionError, cancellation: ct).ConfigureAwait(false);
            return;
        }

        GetTenantByIdQuery query = new(tenantId);
        ErrorOr<TenantResponse> tenantResult = await this.sender.Send(query, ct).ConfigureAwait(false);

        if (tenantResult.IsError)
        {
            ErrorOr<ServiceReadinessResponse> readinessError = tenantResult.Errors;
            await this.SendAsync(readinessError, cancellation: ct).ConfigureAwait(false);
            return;
        }

        TenantDatabaseMetadataResponse? serviceDatabase = tenantResult.Value.Databases
            .FirstOrDefault(database => string.Equals(database.ServiceName, request.ServiceName, StringComparison.OrdinalIgnoreCase));

        bool hasWriteDatabase = !string.IsNullOrWhiteSpace(serviceDatabase?.WriteEnvVarKey);
        bool hasRequiredReadDatabase = serviceDatabase is null ||
            !serviceDatabase.HasSeparateReadDatabase ||
            !string.IsNullOrWhiteSpace(serviceDatabase.ReadEnvVarKey);

        ServiceReadinessResponse response = new()
        {
            Ready = tenantResult.Value.IsActive && serviceDatabase is not null && hasWriteDatabase && hasRequiredReadDatabase,
        };

        await this.SendAsync(response, cancellation: ct).ConfigureAwait(false);
    }
}
