// <copyright file="CheckServiceReadinessEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Customer.Application.Tenants.Features.CheckServiceReadiness.V1;
using Customer.Application.Tenants.Features.GetTenantById.V1;
using Customer.Application.Tenants.Responses;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Customer.Api.Endpoints.V1.Tenants.CheckServiceReadiness;

public sealed class CheckServiceReadinessEndpoint(ISender sender)
    : Endpoint<CheckServiceReadinessRequest, ServiceReadinessResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Get("/admin/Tenants/{TenantId:guid}/Services/{ServiceName}/Readiness");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("tenant", "list");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("admin"));
        });
    }

    public override async Task HandleAsync(CheckServiceReadinessRequest request, CancellationToken ct)
    {
        GetTenantByIdQuery query = new(request.TenantId);
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
