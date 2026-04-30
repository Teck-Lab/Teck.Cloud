// <copyright file="GetTenantDatabaseInfoEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Customer.Application.Tenants.Features.GetCurrentTenantDatabaseInfo.V1;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Customer.Api.Endpoints.V1.Tenants.GetTenantDatabaseInfo;

public sealed class GetTenantDatabaseInfoEndpoint(ISender sender)
    : Endpoint<GetTenantDatabaseInfoRequest, GetCurrentTenantDatabaseInfoResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Get("/admin/Tenants/{TenantId:guid}/database-info");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("tenant", "list");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("admin"));
        });
    }

    public override async Task HandleAsync(GetTenantDatabaseInfoRequest request, CancellationToken ct)
    {
        string serviceName = string.IsNullOrWhiteSpace(request.ServiceName)
            ? "customer"
            : request.ServiceName;

        GetCurrentTenantDatabaseInfoQuery query = new(request.TenantId, serviceName);
        ErrorOr<GetCurrentTenantDatabaseInfoResponse> queryResponse = await this.sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(queryResponse, cancellation: ct).ConfigureAwait(false);
    }
}
