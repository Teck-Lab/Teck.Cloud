// <copyright file="GetTenantByIdEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Customer.Application.Tenants.Features.GetTenantById.V1;
using Customer.Application.Tenants.Responses;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Customer.Api.Endpoints.V1.Tenants.GetTenantById;

public sealed class GetTenantByIdEndpoint(ISender sender) : Endpoint<GetTenantByIdRequest, TenantResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Get("/admin/Tenants/{Id:guid}");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("tenant", "list");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("admin"));
        });
    }

    public override async Task HandleAsync(GetTenantByIdRequest request, CancellationToken ct)
    {
        GetTenantByIdQuery query = new(request.Id);
        ErrorOr<TenantResponse> queryResponse = await sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(queryResponse, cancellation: ct).ConfigureAwait(false);
    }
}
