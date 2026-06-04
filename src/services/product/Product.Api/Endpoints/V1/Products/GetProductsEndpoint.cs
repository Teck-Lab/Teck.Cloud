// <copyright file="GetProductsEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using Product.Application.Product.Features.GetProducts.V1;
using SharedKernel.Core.Pagination;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Product.Api.Endpoints.V1.Products;

/// <summary>
/// Handles get products requests.
/// </summary>
public sealed class GetProductsEndpoint(ISender sender)
    : Endpoint<GetProductsRequest, PagedList<GetProductItemResponse>>
{
    private readonly ISender sender = sender;

    /// <summary>
    /// Configures the endpoint route, version, and access rules.
    /// </summary>
    public override void Configure()
    {
        Get("/Products");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("product", "list");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
    }

    /// <summary>
    /// Handles the incoming request and writes the HTTP response.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(GetProductsRequest request, CancellationToken ct)
    {
        GetProductsQuery query = new(request.Page, request.Size, request.SortBy, request.SortDescending);
        ErrorOr<PagedList<GetProductItemResponse>> result = await this.sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(result, cancellation: ct).ConfigureAwait(false);
    }
}
