// <copyright file="GetPaginatedSuppliersEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Catalog.Application.Suppliers.Features.GetPaginatedSuppliers.V1;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Core.Pagination;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Catalog.Api.Endpoints.V1.Suppliers;

public sealed class GetPaginatedSuppliersEndpoint(ISender sender) : Endpoint<GetPaginatedSuppliersRequest, PagedList<GetPaginatedSuppliersResponse>>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Get("/Suppliers");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("supplier", "list");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
        Validator<GetPaginatedSuppliersValidator>();
    }

    public override async Task HandleAsync(GetPaginatedSuppliersRequest request, CancellationToken ct)
    {
        GetPaginatedSuppliersQuery query = new(request.Page, request.Size, request.Keyword);
        ErrorOr<PagedList<GetPaginatedSuppliersResponse>> queryResponse = await sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(queryResponse, cancellation: ct).ConfigureAwait(false);
    }
}
