// <copyright file="GetProductsByCategoryEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591
using Catalog.Application.Products.Features.GetPaginatedProducts.V1;
using Catalog.Application.Products.Features.GetProductsByCategory.V1;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Catalog.Api.Endpoints.V1.Products;

public sealed class GetProductsByCategoryEndpoint(ISender sender)
    : Endpoint<GetProductsByCategoryRequest, List<GetPaginatedProductsResponse>>
{
    private readonly ISender sender = sender;

    public override void Configure()
    {
        Get("/Categories/{CategoryId:guid}/Products");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("product", "list");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
    }

    public override async Task HandleAsync(GetProductsByCategoryRequest request, CancellationToken ct)
    {
        GetProductsByCategoryQuery query = new(request.CategoryId);
        ErrorOr<List<GetPaginatedProductsResponse>> queryResponse = await this.sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(queryResponse, cancellation: ct).ConfigureAwait(false);
    }
}
