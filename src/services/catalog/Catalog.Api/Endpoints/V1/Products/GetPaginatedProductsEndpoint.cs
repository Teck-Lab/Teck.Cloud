// <copyright file="GetPaginatedProductsEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Catalog.Application.Products.Features.GetPaginatedProducts.V1;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Core.Pagination;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Catalog.Api.Endpoints.V1.Products;

public sealed class GetPaginatedProductsEndpoint(ISender sender)
    : Endpoint<GetPaginatedProductsRequest, PagedList<GetPaginatedProductsResponse>>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Get("/Products");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("product", "list");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
        Validator<GetPaginatedProductsValidator>();
    }

    public override async Task HandleAsync(GetPaginatedProductsRequest request, CancellationToken ct)
    {
        GetPaginatedProductsQuery query = new(request.Page, request.Size, request.Keyword);
        ErrorOr<PagedList<GetPaginatedProductsResponse>> queryResponse = await this.sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(queryResponse, cancellation: ct).ConfigureAwait(false);
    }
}
