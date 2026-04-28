// <copyright file="GetPaginatedTenantsEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Customer.Application.Tenants.Features.GetPaginatedTenants.V1;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Core.Pagination;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Customer.Api.Endpoints.V1.Tenants.GetPaginatedTenants;

public sealed class GetPaginatedTenantsEndpoint(ISender sender)
    : Endpoint<GetPaginatedTenantsRequest, PagedList<GetPaginatedTenantsResponse>>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Get("/admin/Tenants");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("tenant", "list");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("admin"));
        });
        Validator<GetPaginatedTenantsValidator>();
    }

    public override async Task HandleAsync(GetPaginatedTenantsRequest request, CancellationToken ct)
    {
        GetPaginatedTenantsQuery query = new(request.Page, request.Size, request.Keyword, request.Plan, request.IsActive);
        ErrorOr<PagedList<GetPaginatedTenantsResponse>> queryResponse = await this.sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(queryResponse, cancellation: ct).ConfigureAwait(false);
    }
}
