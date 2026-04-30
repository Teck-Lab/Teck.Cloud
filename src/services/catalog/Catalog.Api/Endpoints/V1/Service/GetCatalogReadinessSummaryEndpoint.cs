// <copyright file="GetCatalogReadinessSummaryEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Catalog.Application.Service.Features.GetCatalogReadinessSummary.V1;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Catalog.Api.Endpoints.V1.Service;

public sealed class GetCatalogReadinessSummaryEndpoint(ISender sender)
    : Endpoint<GetCatalogReadinessSummaryRequest, GetCatalogReadinessSummaryResponse>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Get("/Service/Readiness/Summary");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("product", "list");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
    }

    public override async Task HandleAsync(GetCatalogReadinessSummaryRequest request, CancellationToken ct)
    {
        GetCatalogReadinessSummaryQuery query = new();
        ErrorOr<GetCatalogReadinessSummaryResponse> queryResponse = await this.sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(queryResponse, cancellation: ct).ConfigureAwait(false);
    }
}
