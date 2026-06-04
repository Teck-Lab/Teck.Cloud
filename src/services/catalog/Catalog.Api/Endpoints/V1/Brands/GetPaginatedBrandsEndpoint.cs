// <copyright file="GetPaginatedBrandsEndpoint.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>
#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062
using Catalog.Application.Brands.Features.GetPaginatedBrands.V1;
using ErrorOr;
using FastEndpoints;
using Keycloak.AuthServices.Authorization;
using Mediator;
using SharedKernel.Core.Pagination;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;

namespace Catalog.Api.Endpoints.V1.Brands;

/// <summary>
/// Handles get paginated brands requests.
/// </summary>
public sealed class GetPaginatedBrandsEndpoint(ISender sender) : Endpoint<GetPaginatedBrandsRequest, PagedList<GetPaginatedBrandsResponse>>
{
    private readonly ISender sender = sender;

    /// <summary>
    /// Configures the endpoint route, version, and access rules.
    /// </summary>
    public override void Configure()
    {
        Get("/Brands");
        Version(1);
        Options(endpoint =>
        {
            endpoint.RequireProtectedResource("brand", "list");
            endpoint.WithMetadata(new OpenApiAudienceMetadata("public"));
        });
        Validator<GetPaginatedBrandsValidator>();
    }

    /// <summary>
    /// Handles the incoming request and writes the HTTP response.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    public override async Task HandleAsync(GetPaginatedBrandsRequest request, CancellationToken ct)
    {
        GetPaginatedBrandsQuery query = new(request.Page, request.Size, request.Keyword);
        ErrorOr<PagedList<GetPaginatedBrandsResponse>> queryResponse = await sender.Send(query, ct).ConfigureAwait(false);
        await this.SendAsync(queryResponse, cancellation: ct).ConfigureAwait(false);
    }
}
